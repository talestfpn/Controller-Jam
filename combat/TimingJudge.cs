using System;

/// <summary>
/// Resultado discreto de um julgamento de timing.
/// </summary>
public enum TimingResult
{
    Perfect,
    Good,
    Ok,
    Miss,
}

/// <summary>
/// Direção do deslocamento do input em relação ao beat ideal.
/// </summary>
public enum TimingDirection
{
    Early,
    OnTime,
    Late,
}

/// <summary>
/// Dados completos de uma tentativa de timing.
/// </summary>
public readonly struct TimingJudgement
{
    public TimingJudgement(
        TimingResult result,
        double precisionPercent,
        double offsetMilliseconds,
        TimingDirection direction,
        double damageMultiplier,
        double postureMultiplier)
    {
        Result = result;
        PrecisionPercent = precisionPercent;
        OffsetMilliseconds = offsetMilliseconds;
        Direction = direction;
        DamageMultiplier = damageMultiplier;
        PostureMultiplier = postureMultiplier;
    }

    public TimingResult Result { get; }
    public double PrecisionPercent { get; }
    public double OffsetMilliseconds { get; }
    public TimingDirection Direction { get; }
    public double DamageMultiplier { get; }
    public double PostureMultiplier { get; }
    public bool IsHit => Result != TimingResult.Miss;
}

/// <summary>
/// Lógica matemática pura do timing. Não depende de Node, cena ou elemento visual.
/// </summary>
public static class TimingJudge
{
    private const double OnTimeToleranceMilliseconds = 0.5d;

    public static TimingJudgement Judge(
        double inputTime,
        double targetTime,
        double beatDuration,
        TimingConfig config)
    {
        config ??= new TimingConfig();

        var safeBeatDuration = Math.Max(0.001d, beatDuration);
        var windowSeconds = Math.Max(
            0.001d,
            safeBeatDuration * Math.Max(0.001f, config.JudgementWindowBeats));
        var offsetSeconds = inputTime - targetTime;
        var offsetMilliseconds = offsetSeconds * 1000.0d;
        var absoluteOffsetSeconds = Math.Abs(offsetSeconds);
        var normalizedPrecision = Math.Clamp(
            1.0d - (absoluteOffsetSeconds / windowSeconds),
            0.0d,
            1.0d);
        var precisionPercent = normalizedPrecision * 100.0d;

        var direction = offsetMilliseconds < -OnTimeToleranceMilliseconds
            ? TimingDirection.Early
            : offsetMilliseconds > OnTimeToleranceMilliseconds
                ? TimingDirection.Late
                : TimingDirection.OnTime;

        TimingResult result;
        if (absoluteOffsetSeconds > windowSeconds)
        {
            result = TimingResult.Miss;
        }
        else if (normalizedPrecision >= config.PerfectThreshold)
        {
            result = TimingResult.Perfect;
        }
        else if (normalizedPrecision >= config.GoodThreshold)
        {
            result = TimingResult.Good;
        }
        else if (normalizedPrecision >= config.OkThreshold)
        {
            result = TimingResult.Ok;
        }
        else
        {
            result = TimingResult.Miss;
        }

        return new TimingJudgement(
            result,
            precisionPercent,
            offsetMilliseconds,
            direction,
            GetDamageMultiplier(result, config),
            GetPostureMultiplier(result, config));
    }

    public static TimingJudgement JudgeByCircleSize(
        double inputTime,
        double targetTime,
        double beatDuration,
        TimingConfig config,
        double circleSizePercent)
    {
        config ??= new TimingConfig();

        var safeCircleSizePercent = Math.Max(0.0d, circleSizePercent);
        var offsetMilliseconds = (inputTime - targetTime) * 1000.0d;
        var direction = offsetMilliseconds < -OnTimeToleranceMilliseconds
            ? TimingDirection.Early
            : offsetMilliseconds > OnTimeToleranceMilliseconds
                ? TimingDirection.Late
                : TimingDirection.OnTime;

        TimingResult result;
        if (safeCircleSizePercent > config.MaximumAcceptedCircleSizePercent)
        {
            result = TimingResult.Miss;
        }
        else if (safeCircleSizePercent >= config.PerfectThreshold * 100.0d)
        {
            result = TimingResult.Perfect;
        }
        else if (safeCircleSizePercent >= config.GoodThreshold * 100.0d)
        {
            result = TimingResult.Good;
        }
        else if (safeCircleSizePercent >= config.OkThreshold * 100.0d)
        {
            result = TimingResult.Ok;
        }
        else
        {
            result = TimingResult.Miss;
        }

        return new TimingJudgement(
            result,
            safeCircleSizePercent,
            offsetMilliseconds,
            direction,
            GetDamageMultiplier(result, config),
            GetPostureMultiplier(result, config));
    }

    public static string GetDisplayName(TimingResult result)
    {
        return result switch
        {
            TimingResult.Perfect => "PERFECT",
            TimingResult.Good => "GOOD",
            TimingResult.Ok => "OK",
            _ => "MISS",
        };
    }

    public static string GetDirectionDisplayName(TimingDirection direction)
    {
        return direction switch
        {
            TimingDirection.Early => "EARLY",
            TimingDirection.Late => "LATE",
            _ => "ON TIME",
        };
    }

    public static string FormatOffsetMilliseconds(double offsetMilliseconds)
    {
        var roundedOffset = Math.Round(offsetMilliseconds, MidpointRounding.AwayFromZero);
        return roundedOffset >= 0.0d
            ? $"+{roundedOffset:0}ms"
            : $"{roundedOffset:0}ms";
    }

    public static double GetDamageMultiplier(TimingResult result, TimingConfig config)
    {
        return result == TimingResult.Perfect
            ? config.PerfectDamageMultiplier
            : 0.0d;
    }

    public static double GetPostureMultiplier(TimingResult result, TimingConfig config)
    {
        return result switch
        {
            TimingResult.Perfect => config.PerfectPostureMultiplier,
            TimingResult.Good => config.GoodPostureMultiplier,
            TimingResult.Ok => config.OkPostureMultiplier,
            _ => 0.0d,
        };
    }
}
