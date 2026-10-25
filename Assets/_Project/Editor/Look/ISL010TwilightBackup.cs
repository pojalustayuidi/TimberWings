// Assets/_Project/Editor/Look/ISL010TwilightBackup.cs
// Снимок только тех полей CloudCandyStyle, которые меняет ISL-010.
using UnityEngine;

public sealed class ISL010TwilightBackup : ScriptableObject
{
    public CloudCandyStyle style;
    public bool captured;

    public Color sunHorizon;
    public Color sunLow;
    public Color ambientDuskSky;
    public Color ambientDuskEquator;
    public Color ambientDuskGround;
    public Color fogDusk;
    public Color zenithDusk;
    public Color horizonDusk;
    public Color gradeDuskFilter;

    public Color moonColor;
    public Color ambientNightSky;
    public Color ambientNightEquator;
    public Color ambientNightGround;
    public Color fogNight;
    public Color gradeNightFilter;

    public void Capture(CloudCandyStyle s)
    {
        style = s;
        sunHorizon = s.sunHorizon;
        sunLow = s.sunLow;
        ambientDuskSky = s.ambientDusk.sky;
        ambientDuskEquator = s.ambientDusk.equator;
        ambientDuskGround = s.ambientDusk.ground;
        fogDusk = s.fogDusk;
        zenithDusk = s.zenithDusk;
        horizonDusk = s.horizonDusk;
        gradeDuskFilter = s.gradeDusk.filter;

        moonColor = s.moonColor;
        ambientNightSky = s.ambientNight.sky;
        ambientNightEquator = s.ambientNight.equator;
        ambientNightGround = s.ambientNight.ground;
        fogNight = s.fogNight;
        gradeNightFilter = s.gradeNight.filter;
        captured = true;
    }

    public void Restore(CloudCandyStyle s)
    {
        s.sunHorizon = sunHorizon;
        s.sunLow = sunLow;
        s.ambientDusk.sky = ambientDuskSky;
        s.ambientDusk.equator = ambientDuskEquator;
        s.ambientDusk.ground = ambientDuskGround;
        s.fogDusk = fogDusk;
        s.zenithDusk = zenithDusk;
        s.horizonDusk = horizonDusk;
        s.gradeDusk.filter = gradeDuskFilter;

        s.moonColor = moonColor;
        s.ambientNight.sky = ambientNightSky;
        s.ambientNight.equator = ambientNightEquator;
        s.ambientNight.ground = ambientNightGround;
        s.fogNight = fogNight;
        s.gradeNight.filter = gradeNightFilter;
    }
}
