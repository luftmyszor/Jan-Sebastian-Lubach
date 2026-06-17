using System;
using System.Buffers;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

// Added IDisposable so we can safely clean up the ThreadLocal memory when the solver finishes
public class GeneticAlgorithm : IDisposable
{
    private Genome[] _population;
    
    // ZMIANA 1: Zmiana pojedynczego ewaluatora na ThreadLocal
    private readonly ThreadLocal<FitnessEvaluator> _evaluators;
    
    private readonly int _populationSize;
    private readonly int _genomeLength;
    private readonly float _mutationRate;
    private readonly int _elitismCount;
    private readonly float _parentSelectionPercentage;
    private readonly int _breedablePoolSize;
    private readonly TimetableMapper _mapper;
    private readonly int _immigrationCount;
    private int _stagnationCounter = 0;
    private float _lastBestFitness = float.MinValue;

    public int[] GetBestGenes()
    {
        return _population[0].Genes;
    }
    private bool _safeguardActive = false;

    public GeneticAlgorithm(int popSize, float mutRate, int elite, TimetableMapper mapper, int immigrationCount = 50, float parentSelectionPercentage = 0.3f)
    {
        _populationSize = popSize;
        _mutationRate = mutRate;
        _elitismCount = elite;
        _mapper = mapper;
        _parentSelectionPercentage = parentSelectionPercentage;
        _immigrationCount = immigrationCount;
        
        _breedablePoolSize = Math.Max(elite + 1, (int)(_populationSize * parentSelectionPercentage));

        _genomeLength = mapper.GenomeLength; 
        _population = new Genome[_populationSize];
        
        // ZMIANA 2: Inicjalizacja ThreadLocal. Każdy wątek CPU dostanie swoją własną kopię ewaluatora.
        _evaluators = new ThreadLocal<FitnessEvaluator>(() => new FitnessEvaluator(mapper)); 
        
        
        InitializePopulation();
    }

    // --- MAIN LOOP ---
    public void Run(int generations)
    {
        for (int generation = 0; generation < generations; generation++)
        {
            EvaluatePopulation();
            SortPopulationByFitness();
            
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
        Parallel.For(0, _populationSize, i =>
        {
            if (!_population[i].IsEvaluated)
            {
                // Przekazujemy stan Safeguard do ewaluatora
                var fitness = _evaluators.Value.Evaluate(_population[i], _safeguardActive);
                _population[i].Fitness = fitness;
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

        // 4A. Elitism
        for (int i = 0; i < _elitismCount; i++)
        {
            nextGeneration[i] = _population[i];
        }

        // 4B. Breed 
        int breedLimit = _populationSize - _immigrationCount;
        for (int i = _elitismCount; i < breedLimit; i++)
        {
            Genome parentA = SelectParentTournament();
            Genome parentB = SelectParentTournament();

            Genome child = Crossover(parentA, parentB);
            Mutate(ref child);

            nextGeneration[i] = child;
        }

        // 4C. IMMIGRATION
        for (int i = breedLimit; i < _populationSize; i++)
        {
            Genome freshSeed = _mapper.CreateSmartSeedGenome(Random.Shared);
            int[] rentedGenes = ArrayPool<int>.Shared.Rent(_genomeLength);
            freshSeed.Genes.CopyTo(rentedGenes, 0);
            nextGeneration[i] = new Genome(rentedGenes);
        }

        // 4D. Memory Cleanup
        for (int i = _elitismCount; i < _populationSize; i++)
        {
            ArrayPool<int>.Shared.Return(_population[i].Genes);
        }

        _population = nextGeneration;
    }

    // --- STEP 5: SELECTION ---
    private Genome SelectParentTournament()
    {
        int tournamentSize = 5;
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

    // --- STEP 6: CROSSOVER (Two-Point Crossover) ---
    private Genome Crossover(Genome parentA, Genome parentB)
    {
        int[] childGenes = ArrayPool<int>.Shared.Rent(_genomeLength);
        
        // Zamiast miksować 50/50 (co niszczy całe dni), wycinamy jeden ciągły blok genów 
        // od rodzica A i wsadzamy go w środek planu rodzica B.
        int point1 = Random.Shared.Next(0, _genomeLength);
        int point2 = Random.Shared.Next(point1, _genomeLength);

        for (int i = 0; i < _genomeLength; i++)
        {
            if (i >= point1 && i < point2)
                childGenes[i] = parentA.Genes[i];
            else
                childGenes[i] = parentB.Genes[i];
        }

        return new Genome(childGenes);
    }

    // --- STEP 7: MUTATION (Adaptive "Earthquake" Mutation) ---
    private void Mutate(ref Genome child)
    {
        // Trzęsienie ziemi (earthquake) próbuje zniszczyć zator. 
        // ALE! Jeśli działa Safeguard, wyłączamy trzęsienia, żeby algorytm mógł
        // spokojnie i precyzyjnie poukładać okienka studentów bez ciągłego wybuchania.
        bool earthquake = _stagnationCounter > 40 && !_safeguardActive;

        for (int i = 0; i < _genomeLength; i++)
        {
            bool isBroken = child.IsBroken(i);
            
            float effectiveMutationRate = isBroken 
                ? (earthquake ? 0.95f : 0.50f) 
                : (earthquake ? _mutationRate * 5f : _mutationRate);

            if (Random.Shared.NextDouble() < effectiveMutationRate)
            {
                child.Genes[i] = _mapper.CreateSingleValidGene(i, Random.Shared);
                child.IsEvaluated = false; 
            }
        }
    }
    public void RunDiagnostics()
    {
        // Używa pierwszego dostępnego ewaluatora, by zdiagnozować najlepszy genom
        _evaluators.Value.AnalyzeViolations(_population[0]);
    }
    public float GetBestFitness() => _population[0].Fitness;

    public float GetWorstFitness()
    {
        for (int i = _populationSize - 1; i >= 0; i--)
        {
            if (_population[i].IsEvaluated) return _population[i].Fitness;
        }
        return 0f; 
    }

    public float GetAverageFitness()
    {
        float totalFitness = 0;
        int evaluatedCount = 0;
        for (int i = 0; i < _populationSize; i++)
        {
            if (_population[i].IsEvaluated)
            {
                totalFitness += _population[i].Fitness;
                evaluatedCount++;
            }
        }
        return evaluatedCount > 0 ? totalFitness / evaluatedCount : 0f;
    }

    public void StepGeneration()
    {
        bool wasSafeguardActive = _safeguardActive;
        
        // Odpalamy Safeguard, jeśli utknęliśmy od 100 generacji
        _safeguardActive = _stagnationCounter > 100;

        // Jeśli tryb właśnie się aktywował, musimy zresetować wszystkie oceny, 
        // by ewaluator przeliczył je od nowa z uwzględnieniem bonusów za Soft Constraints
        if (_safeguardActive && !wasSafeguardActive)
        {
            Console.WriteLine("\n[!] SAFEGUARD AKTYWNY: Brak możliwości naprawy hard constraints.");
            Console.WriteLine("    Przełączam silnik na optymalizację Soft Constraints (okienka, preferencje) uszkodzonego planu...\n");
            
            for (int i = 0; i < _populationSize; i++)
            {
                _population[i].IsEvaluated = false;
            }
        }

        EvaluatePopulation();
        SortPopulationByFitness();
        
        float currentBest = _population[0].Fitness;
        
        // Śledzenie stagnacji
        if (Math.Abs(currentBest - _lastBestFitness) < 0.001f)
        {
            _stagnationCounter++;
        }
        else
        {
            _stagnationCounter = 0;
            _lastBestFitness = currentBest;
            _safeguardActive = false; // Wyłączamy safeguard, jeśli algorytm naturalnie poszedł do przodu!
        }

        CreateNextGeneration(); 
    }
    // ZMIANA 4: Czyszczenie pamięci po zakończeniu algorytmu
    public void Dispose()
    {
        _evaluators?.Dispose();
    }
}