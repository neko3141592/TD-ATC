using UnityEngine;

public readonly struct ComplexValue
{
    public readonly float Real;
    public readonly float Imag;

    public ComplexValue(float real, float imag)
    {
        Real = real;
        Imag = imag;
    }

    public static implicit operator ComplexValue(float real)
    {
        return new ComplexValue(real, 0);
    }

    public static readonly ComplexValue Zero = new ComplexValue(0f, 0f);

    public static readonly ComplexValue One = new ComplexValue(1f, 0f);

    public static readonly ComplexValue J = new ComplexValue(0f, 1f);

    public float Magnitude => Mathf.Sqrt(Real * Real + Imag * Imag);
    public float PhaseRad => Mathf.Atan2(Imag, Real);
    public float PhaseDeg => PhaseRad * Mathf.Rad2Deg;

    public static ComplexValue operator +(ComplexValue a, ComplexValue b)
    {
        return new ComplexValue(a.Real + b.Real, a.Imag + b.Imag);
    }

    public static ComplexValue operator -(ComplexValue a, ComplexValue b)
    {
        return new ComplexValue(a.Real - b.Real, a.Imag - b.Imag);
    }

    public static ComplexValue operator *(ComplexValue a, ComplexValue b)
    {
        return new ComplexValue(
            a.Real * b.Real - a.Imag * b.Imag,
            a.Real * b.Imag + a.Imag * b.Real
        );
    }

    public static ComplexValue operator *(float scalar, ComplexValue value)
    {
        return new ComplexValue(
            value.Real * scalar, value.Imag * scalar
        );
    }

    public static ComplexValue operator /(ComplexValue a, ComplexValue b)
    {
        float denominator = b.Real * b.Real + b.Imag * b.Imag;

        if (denominator <= 0f)
        {
            return new ComplexValue(0f, 0f);
        }

        return new ComplexValue(
            (a.Real * b.Real + a.Imag * b.Imag) / denominator,
            (a.Imag * b.Real - a.Real * b.Imag) / denominator
        );
    }

    public static ComplexValue operator /(float scalar, ComplexValue value)
    {
        return new ComplexValue(scalar, 0f) / value;
    }

    public static ComplexValue FromPolar(float magnitude, float phaseRad)
    {
        return new ComplexValue(
            magnitude * Mathf.Cos(phaseRad),
            magnitude * Mathf.Sin(phaseRad)
        );
    }

    public ComplexValue Rotated(float angleRad)
    {
        return this * FromPolar(1f, angleRad);
    }


    public override string ToString()
    {
        return $"{Real:F3} {(Imag >= 0f ? "+" : "-")} j{Mathf.Abs(Imag):F3}";
    }
}
