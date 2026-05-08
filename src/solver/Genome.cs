using System;


public record struct Genome
{
    public int[] Genes;
    public float Fitness;
    public bool IsEvaluated;

    public Genome(int[] genes)
    {
        Genes = genes;
        Fitness = 0f;
        IsEvaluated = false;
    }
}

public class FitnessEvaluator
{
    public float Evaluate(int[] genes)
    {
        // TODO: Decode packed integers and run Hard/Soft constraints
        // Returning a random dummy fitness for now.
        return Random.Shared.Next(0, 100); 
    }
}