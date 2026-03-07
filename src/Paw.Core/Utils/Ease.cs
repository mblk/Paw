using System.Net.NetworkInformation;

namespace Paw.Core.Utils;

public static class Ease
{
    public static float Linear(float input)
    {
        if (input <= 0f)
            return 0f;

        if (input >= 1f)
            return 1f;
            
        return input;
    }

    /// <summary>
    /// [0..1] => [0..1]
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static float InOutExpo(float input)
    {
        if (input <= 0.0f)
            return 0.0f;

        if (input >= 1.0f)
            return 1.0f;

        if (input < 0.5f)
            return MathF.Pow(2.0f, 20.0f * input - 10.0f) / 2.0f;

        return (2.0f - MathF.Pow(2.0f, -20.0f * input + 10.0f)) / 2.0f;
    }

    public static float InOutQuad(float input)
    {
        if (input < 0.5f)
            return 2f * input * input;
        
        return 1f - MathF.Pow(-2f * input + 2, 2f) / 2f;

        // function easeInOutQuad(x: number): number {
        // return x < 0.5 ? 2 * x * x : 1 - Math.pow(-2 * x + 2, 2) / 2;
    }

    public static float InOutCubic(float input)
    {
        if (input < 0.5f)
            return 4f * input * input * input;

        return 1f - MathF.Pow(-2f * input + 2f, 3f) / 2f; 
 
        //return x < 0.5 ? 4 * x * x * x : 1 - Math.pow(-2 * x + 2, 3) / 2;

    }

    /// <summary>
    /// [-1..1] => [-1..1]
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static float Mirror(float input, Func<float, float> func)
    {
        if (input < 0f)
            return -func(-input);
        return func(input);
    }
}