# Radio presets.


Each json file in the list maps to a radio model.
The name of the file is the preset name, mapped through the `model` entry in the lua.

## Available effects
### Gain

Express a gain, in dB, on the source.
Positive for amplification, negative for attenuation.

```json
{
    "$type": "gain",
    "gain": 32
}
```

### CVSD

Applies a CVSD encoding effect on the source.
```json
{
    "$type": "cvsd"
}
```

### Filters

Applies a set of DSP filters on the source.

Available DSP filters are lowpass, highpass, and peak (think narrow bandpass).
If no q factor are specified, it's a first order pass.

```json
{
    "$type": "filters",
    "filters": [
        {
            "$type": "lowpass",
            "frequency": 1234,
            "q": 0.2
        }
    ]
}
```

### Compressor
```json
{
    "$type": "compressor",
    "attack": 0.01,
    "makeUp": 6,
    "release": 0.2,
    "threshold": -33,
    "ratio": 0.85
}
```

gain and threshold expressed in dB.
Translating from DCS' presets, ratio = 1 / slope, or maybe 1 / (1 - slope).
attack and release time are expressed in seconds.

### Sidechain Compressor
```json
{
    "$type": "sidechainCompressor",
    "attack": 0.01,
    "makeUp": 6,
    "release": 0.2,
    "threshold": -33,
    "ratio": 1.18,
    "sidechainEffect": {
      "$type": "filters",
      "filters": [
        {
          "$type": "highpass",
          "frequency": 709
        }
      ]
    }
}
```

gain and threshold expressed in dB.
Translating from DCS' presets, ratio = 1 / slope, or maybe 1 / (1 - slope).
attack and release time are expressed in seconds.

sidechain is an effects chain applied on the same input signal that will go through the compressor.
That signal is only used to drive the compressor logic.

### Saturation
```json
{
    "$type": "saturation",
    "gain": 11,
    "threshold": -30
}
```

gain and threshold expressed in dB.

### Chain

Applies a chain of effects, in the order they appear in the json, from top to bottom.
It enables complex effects to be built.

```json
{
    "$type": "chain",
    "effects": [
        {
            "$type": "saturation",
            "gain": 11,
            "threshold": -30
        },
        {
            "$type": "gain",
            "gain": -3
        },
        {
            "$type": "filters",
            "filters": [
                {
                    "$type": "lowpass",
                    "frequency": 1234
                },
                {
                    "$type": "highpass",
                    "frequency": 64,
                    "q": 0.1
                }
            ]
        }
    ]
}
```

Would apply a saturation, then an attenuation, then run a pass of filters with a first order lowpass then a highpass with a Q factor of 0.1.
