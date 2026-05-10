using System;
using System.Buffers;
using System.Linq;
using System.Threading.Tasks;

public class GeneticAlgorithm
{
    private Genome[] _population;
    private readonly FitnessEvaluator _evaluator;
    
    private readonly int _populationSize;
    private readonly int _genomeLength;
    private readonly float _mutationRate;
    private readonly int _elitismCount;
    private readonly float _parentSelectionPercentage;
    private readonly int _breedablePoolSize;
    private readonly TimetableMapper _mapper;

    // Pass the mapper into the GA constructor
    public GeneticAlgorithm(int popSize, float mutRate, int elite, TimetableMapper mapper, float parentSelectionPercentage = 0.3f)
    {
        _populationSize = popSize;
        _mutationRate = mutRate;
        _elitismCount = elite;
        _mapper = mapper;
        _parentSelectionPercentage = parentSelectionPercentage;
        
        // Calculate how many of the top individuals are eligible for breeding
        _breedablePoolSize = Math.Max(elite + 1, (int)(_populationSize * parentSelectionPercentage));

        _genomeLength = mapper.GenomeLength; 
        _population = new Genome[_populationSize];
        _evaluator = new FitnessEvaluator(mapper); 
        InitializePopulation();
    }

    

    // --- MAIN LOOP ---
    public void Run(int generations)
    {

        for (int generation = 0; generation < generations; generation++)
        {
            EvaluatePopulation();
            SortPopulationByFitness();
            
            // Check termination condition (perfect score)
            if (_population[0].Fitness >= 99.9f) 
            {
                Console.WriteLine($"Perfect solution found at generation {generation}!");
                break;
            }

            CreateNextGeneration();
        }
    }

    // --- STEP 1: INITIALIZATION ---
    private void InitializePopulation()
    {
        for (int i = 0; i < _populationSize; i++)
        {
            Genome seed = _mapper.CreateSmartSeedGenome(Random.Shared);
            
            int[] rentedGenes = ArrayPool<int>.Shared.Rent(_genomeLength);
            
            seed.Genes.CopyTo(rentedGenes, 0);
            
            _population[i] = new Genome(rentedGenes);
        }
    }

    // --- STEP 2: EVALUATION ---
    private void EvaluatePopulation()
    {
        // Run fitness evaluation on all CPU cores simultaneously
        Parallel.For(0, _populationSize, i =>
        {
            if (!_population[i].IsEvaluated)
            {
                _population[i].Fitness = _evaluator.Evaluate(_population[i].Genes);
                _population[i].IsEvaluated = true;
            }
        });
    }

    // --- STEP 3: SORTING ---
    private void SortPopulationByFitness()
    {
        Array.Sort(_population, (a, b) => b.Fitness.CompareTo(a.Fitness));
    }

    // --- STEP 4: ORCHESTRATE NEXT GENERATION ---
    private void CreateNextGeneration()
    {
        Genome[] nextGeneration = new Genome[_populationSize];

        // 4A. Elitism: Copy the absolute best directly to the next generation
        for (int i = 0; i < _elitismCount; i++)
        {
            nextGeneration[i] = _population[i];
        }

        // 4B. Breed the rest of the population
        for (int i = _elitismCount; i < _populationSize; i++)
        {
            Genome parentA = SelectParentTournament();
            Genome parentB = SelectParentTournament();

            Genome child = Crossover(parentA, parentB);
            Mutate(ref child);

            nextGeneration[i] = child;
        }

        // 4C. Memory Cleanup: Return old, unused arrays to the pool
        for (int i = _elitismCount; i < _populationSize; i++)
        {
            // Don't return elite arrays
            ArrayPool<int>.Shared.Return(_population[i].Genes);
        }

        _population = nextGeneration;
    }

    // --- STEP 5: SELECTION ---
    private Genome SelectParentTournament()
    {
        int tournamentSize = 3;
        // Select randomly from the top parentSelectionPercentage of the population
        Genome best = _population[Random.Shared.Next(0, _breedablePoolSize)];

        for (int i = 1; i < tournamentSize; i++)
        {
            Genome contender = _population[Random.Shared.Next(0, _breedablePoolSize)];
            if (contender.Fitness > best.Fitness)
            {
                best = contender;
            }
        }
        return best;
    }

    // --- STEP 6: CROSSOVER ---
    private Genome Crossover(Genome parentA, Genome parentB)
    {
        // Rent a brand new array for the child
        int[] childGenes = ArrayPool<int>.Shared.Rent(_genomeLength);
        
        int splitPoint = _genomeLength / 2;

        // Zero-allocation memory slicing using Span<T>
        Span<int> spanA = parentA.Genes.AsSpan();
        Span<int> spanB = parentB.Genes.AsSpan();
        Span<int> childSpan = childGenes.AsSpan();

        spanA.Slice(0, splitPoint).CopyTo(childSpan.Slice(0, splitPoint));
        spanB.Slice(splitPoint, _genomeLength - splitPoint).CopyTo(childSpan.Slice(splitPoint, _genomeLength - splitPoint));

        return new Genome(childGenes);
    }

    // --- STEP 7: MUTATION ---
    private void Mutate(ref Genome child)
    {
        for (int i = 0; i < _genomeLength; i++)
        {
            if (Random.Shared.NextDouble() < _mutationRate)
            {
                // TODO change bounds
                child.Genes[i] = Random.Shared.Next(1000, 9999); 
                
                // If mutated, we must re-evaluate it next round
                child.IsEvaluated = false; 
            }
        }
    }
    /// <summary>
    /// Returns the highest fitness score in the current population.
    /// </summary>
    public float GetBestFitness()
    {
        return _population[0].Fitness;
    }

    /// <summary>
    /// Returns the average fitness score of the current population.
    /// </summary>
    public float GetAverageFitness()
    {
        float totalFitness = 0;
        for (int i = 0; i < _populationSize; i++)
        {
            totalFitness += _population[i].Fitness;
        }
        return totalFitness / _populationSize;
    }

    /// <summary>
    /// Runs a single generation cycle. 
    /// This replaces the internal 'for' loop in the Run() method so you can control it externally.
    /// </summary>
    public void StepGeneration()
    {
        EvaluatePopulation();
        
        SortPopulationByFitness();
        
        CreateNextGeneration(); 
    }
}