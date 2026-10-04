# Diagnostic series index

All values are wall-clock frame times at 3456×2160, VSync1/120Hz. Focus losses invalidate the entire sample. Prototype results are not a selected shipping change.

| Series/sample | Mean ms | FPS | p95 ms | p99 ms | Max ms | >25 ms | Focus valid |
|---|---:|---:|---:|---:|---:|---:|---|
| groups/hall-baseline | 17.40 | 57.5 | 25.96 | 30.91 | 31.63 | 33 | yes |
| groups/hall-lower-point | 13.40 | 74.6 | 22.69 | 25.19 | 25.25 | 13 | yes |
| groups/hall-lower-spot | 15.40 | 64.9 | 25.08 | 26.76 | 33.10 | 30 | yes |
| groups/hall-upper-point | 14.31 | 69.9 | 22.32 | 25.18 | 25.37 | 11 | yes |
| groups/hall-upper-spot | 14.90 | 67.1 | 24.81 | 26.20 | 31.01 | 23 | yes |
| groups/hall-stairs-point | 17.28 | 57.9 | 25.33 | 30.91 | 38.94 | 34 | yes |
| groups/hall-stairs-spot | 18.07 | 55.3 | 30.09 | 35.48 | 38.56 | 93 | yes |
| groups/upper-baseline | 15.81 | 63.2 | 28.84 | 30.39 | 34.07 | 71 | yes |
| groups/upper-lower-point | 13.77 | 72.6 | 17.39 | 19.20 | 20.90 | 0 | REJECTED |
| groups/upper-lower-spot | 17.31 | 57.8 | 21.60 | 23.52 | 26.09 | 1 | REJECTED |
| groups/upper-upper-point | 13.86 | 72.1 | 18.08 | 19.81 | 21.75 | 0 | REJECTED |
| groups/upper-upper-spot | 15.58 | 64.2 | 19.75 | 21.63 | 25.83 | 1 | REJECTED |
| groups/upper-stairs-point | 17.51 | 57.1 | 22.62 | 27.01 | 43.90 | 7 | REJECTED |
| groups/upper-stairs-spot | 17.92 | 55.8 | 22.38 | 24.80 | 30.42 | 3 | REJECTED |
| variants/hall-baseline | 17.48 | 57.2 | 25.36 | 28.98 | 29.57 | 31 | yes |
| variants/upper-baseline | 15.24 | 65.6 | 24.71 | 26.93 | 32.03 | 21 | yes |
| variants/hall-bounded | 8.85 | 113.0 | 12.37 | 13.66 | 17.52 | 0 | yes |
| variants/upper-bounded | 9.15 | 109.3 | 12.83 | 13.17 | 16.80 | 0 | yes |
| variants/hall-bounded-soft | 9.28 | 107.7 | 12.80 | 16.88 | 21.20 | 0 | yes |
| variants/upper-bounded-soft | 9.75 | 102.5 | 13.53 | 16.86 | 19.49 | 0 | yes |
| variants/hall-sparse-fill | 10.26 | 97.5 | 14.00 | 15.91 | 18.37 | 0 | REJECTED |
| variants/upper-sparse-fill | 9.77 | 102.4 | 11.53 | 12.97 | 21.00 | 0 | REJECTED |
| bright-variants/hall-bright-fill | 9.19 | 108.9 | 12.61 | 16.84 | 17.38 | 0 | yes |
| bright-variants/upper-bright-fill | 9.69 | 103.2 | 12.95 | 16.91 | 22.94 | 0 | yes |
| bright-variants/hall-bright-tight | 8.90 | 112.4 | 11.68 | 12.90 | 23.77 | 0 | yes |
| bright-variants/upper-bright-tight | 9.02 | 110.9 | 11.89 | 13.03 | 31.46 | 1 | yes |
| bright-variants/hall-bright-spot | 9.37 | 106.8 | 12.57 | 16.81 | 20.69 | 0 | yes |
| bright-variants/upper-bright-spot | 9.74 | 102.6 | 16.34 | 16.92 | 18.40 | 0 | yes |
| spatial-variants/hall-lowered-fill | 10.25 | 97.6 | 14.34 | 18.89 | 21.36 | 0 | yes |
| spatial-variants/upper-lowered-fill | 10.20 | 98.0 | 14.26 | 19.30 | 22.79 | 0 | yes |
| spatial-variants/hall-lowered-soft | 10.51 | 95.2 | 15.19 | 20.16 | 24.41 | 0 | yes |
| spatial-variants/upper-lowered-soft | 10.69 | 93.6 | 15.86 | 21.35 | 27.54 | 1 | yes |
| spatial-variants/hall-checker-fill | 10.06 | 99.4 | 13.89 | 15.79 | 21.47 | 0 | yes |
| spatial-variants/upper-checker-fill | 10.10 | 99.0 | 14.03 | 16.58 | 20.86 | 0 | yes |
| balanced-variants/hall-balanced | 10.97 | 91.2 | 15.50 | 20.61 | 25.31 | 2 | yes |
| balanced-variants/upper-balanced | 11.37 | 87.9 | 14.60 | 16.83 | 29.46 | 1 | yes |
| balanced-variants/hall-balanced-wide | 12.61 | 79.3 | 20.38 | 25.24 | 33.17 | 11 | yes |
| balanced-variants/upper-balanced-wide | 12.33 | 81.1 | 18.20 | 23.83 | 29.32 | 5 | yes |
| balanced-variants/hall-balanced-tight | 10.17 | 98.3 | 16.60 | 16.94 | 23.97 | 0 | yes |
| balanced-variants/upper-balanced-tight | 10.67 | 93.8 | 16.93 | 20.86 | 33.39 | 2 | yes |
| cone-variants/hall-baseline | 17.91 | 55.8 | 25.26 | 29.03 | 30.70 | 32 | yes |
| cone-variants/upper-baseline | 15.58 | 64.2 | 25.08 | 25.33 | 28.19 | 29 | yes |
| cone-variants/hall-wide-core | 11.12 | 89.9 | 14.48 | 17.07 | 29.39 | 1 | yes |
| cone-variants/upper-wide-core | 11.48 | 87.1 | 16.84 | 17.43 | 25.45 | 3 | yes |
| cone-variants/hall-tight-core | 10.34 | 96.7 | 13.29 | 16.95 | 20.54 | 0 | yes |
| cone-variants/upper-tight-core | 10.96 | 91.3 | 16.74 | 17.30 | 37.66 | 2 | yes |
| cone-variants/hall-short-core | 9.63 | 103.9 | 13.13 | 17.02 | 21.22 | 0 | yes |
| cone-variants/upper-short-core | 13.39 | 74.7 | 17.30 | 18.78 | 24.73 | 0 | REJECTED |
| cone-variants/repeat-upper-baseline | 19.08 | 52.4 | 24.87 | 29.69 | 36.21 | 19 | REJECTED |
| cone-variants/repeat-upper-lower-point | 14.84 | 67.4 | 18.85 | 26.28 | 68.22 | 6 | REJECTED |
| cone-variants/repeat-upper-lower-spot | 19.45 | 51.4 | 28.69 | 73.86 | 969.48 | 25 | REJECTED |
| cone-variants/repeat-upper-upper-point | 14.42 | 69.3 | 19.33 | 26.17 | 141.31 | 8 | REJECTED |
| cone-variants/repeat-upper-upper-spot | 13.69 | 73.0 | 14.60 | 15.37 | 23.25 | 0 | REJECTED |
| cone-variants/repeat-upper-stairs-point | 20.38 | 49.1 | 27.85 | 48.84 | 500.41 | 34 | REJECTED |
| cone-variants/repeat-upper-stairs-spot | 15.79 | 63.3 | 17.02 | 17.42 | 20.59 | 0 | REJECTED |
