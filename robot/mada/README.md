# Dati macchina del robot reale

Copia in sola lettura dei dati macchina (Mada) del KRC2, presi dal backup del
14/07/2026 (`KRC\ROBOTER\KRC\R1\MADA`). Non modificarli qui: servono come
riferimento per il gemello digitale.

| File | Contenuto |
|---|---|
| `$machine.dat` | trasformazione, fine corsa software, versi assi, rapporti di riduzione, velocità motori |
| `$robcor.dat` | correzioni di modello del robot |

## Valori principali

- Trasformazione: `#KR5 C2 FLR ZH02` (KR 5 arc, montaggio a pavimento)
- `$ROBROOT` = 0 (base nell'origine del mondo)

| Asse | `$SOFTN_END` | `$SOFTP_END` | `$AXIS_DIR` | `$MAMES` | `$RAT_MOT_AX` | `$VEL_AXIS_MA` (giri/min) |
|---|---|---|---|---|---|---|
| A1 | -155 | 155 | -1 | 0 | -125/1 | 3950 |
| A2 | -180 | 65 | 1 | -90 | -125/1 | 3950 |
| A3 | -15 | 158 | 1 | 90 | 87/1 | 4400 |
| A4 | -350 | 350 | -1 | 0 | -670/9 | 4400 |
| A5 | -130 | 130 | -1 | 0 | -380/9 | 4400 |
| A6 | -350 | 350 | -1 | 0 | -410/17 | 4400 |
