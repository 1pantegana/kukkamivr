# Shenzhen Hotel VR

Camera d'albergo a Shenzhen, di notte, con vetrata a tutta parete sul fiume e
sullo skyline. Progetto **Unity 6000.5.4f1 + URP**, per **Meta Quest 3 collegato
al PC via SteamVR** (non standalone: il gioco gira sulla GPU del PC).

![vista dalla poltrona](Screenshots/02-dalla-poltrona.png)

## Come si prova

1. **Unity Hub → Add → Add project from disk** → seleziona questa cartella.
2. Apri `Assets/Scenes/ShenzhenHotel.unity`.
3. In SteamVR: **Impostazioni → Sviluppatore → Imposta SteamVR come runtime
   OpenXR**. Senza questo passo Unity apre il runtime sbagliato e il visore
   resta nero.
4. Collega il Quest 3 (Link, Air Link o Virtual Desktop) e lascia SteamVR aperto.
5. Premi **Play**. Non serve compilare nulla.

Per provare **senza visore**, premi Play e basta: il rig ricade su mouse e
tastiera.

## Comandi

| | Con i controller | Da scrivania |
|---|---|---|
| Camminare | stick sinistro | W A S D |
| Ruotare a scatti (30°) | stick destro, sinistra/destra | Q / E |
| Teletrasporto | tieni premuto il **grilletto destro**, mira, rilascia | barra spaziatrice |
| Guardarsi intorno | muovi la testa | tasto destro del mouse + mouse |

Il teletrasporto atterra solo su pavimento libero: mirando sul letto o sulla
scrivania l'arco diventa rosso. Camminando con lo stick si scivola lungo mobili
e pareti; il movimento fisico reale non e' mai limitato.

## Menu dell'editor

Tutta la scena e' generata da codice, quindi e' ricostruibile e modificabile
senza trascinare oggetti nell'editor.

| Voce di menu | Cosa fa |
|---|---|
| **Hotel VR → 1 - Configura progetto** | colorspace Linear, target Windows x64, OpenXR con i profili controller Touch / Touch Plus |
| **Hotel VR → 2 - Prepara scena** | ricostruisce guscio, vetrata, luci, arredamento, skyline e rig, e salva la scena |
| **Hotel VR → 3 - Verifica scena** | stampa dimensioni, elenco mobili, compenetrazioni, numero di materiali |
| **Hotel VR → 4 - Build PC** | genera `Build/ShenzhenHotelVR.exe` |
| **Hotel VR → 5 - Screenshot** | renderizza 5 inquadrature in `Screenshots/` |
| **Hotel VR → 6 - Genera audio** | risintetizza i WAV in `Assets/Generated/Audio` |
| **Hotel VR → 7 - Verifica occlusion culling** | confronta il render con culling acceso e spento: se differiscono, il bake sta nascondendo qualcosa di visibile |

Le stesse voci girano da riga di comando, utile per rigenerare tutto senza
aprire l'editor:

```bash
"C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe" -projectPath "C:\Users\Gabri\Documents\Claude\kuka vr2" -batchmode -nographics -quit -executeMethod HotelRoomSetup.Setup -logFile -
```

## Com'e' fatto

- **Stanza** 5 × 6 m, alta 3 m, a 35 m dal suolo. Origine al centro del
  pavimento, X = larghezza, Z = profondita', vetrata a Z positivo.
- **Arredamento** (`Assets/Editor/RoomFurnishing.cs`): letto con testata,
  comodini con abat-jour, scrivania, sedia, TV, armadio, porta, poltrona e
  tavolino verso la vetrata, tappeto, pianta, quadro, cestino. La pianta della
  camera e' in una tabella di costanti in cima al file: spostare un mobile e'
  una riga.
- **Geometria** (`Assets/Editor/MeshKit.cs`): scatole con spigoli smussati. Un
  cubo con lo spigolo vivo, in VR, si riconosce subito come primitiva; con
  1-2 cm di smusso che catturano una riga di luce diventa un mobile.
- **Skyline** (`Assets/Editor/CityBuilder.cs`): ~250 torri procedurali, landmark
  riconoscibili (supertall affusolata, torre a gradoni, antenna), fiume con
  riflessi additivi, battelli, scie di fari sul ponte, cielo notturno generato.
  Generato invece che HDRI perche' ci si muove nella stanza e serve la
  parallasse: un panorama a distanza infinita si tradirebbe subito.
- **Rig XR** (`Assets/Scripts/XRRig.cs`): solo API `UnityEngine.XR`, nessuna
  dipendenza dal Meta XR SDK. `OVRCameraRig`/`OVRManager` funzionano solo col
  runtime Oculus nativo e **con SteamVR non tracciano**.
- **Audio** (`Assets/Editor/AudioBaker.cs`): sintetizzato, nessun campione
  scaricato. Il brusio del traffico e' rumore browniano passa-basso modulato da
  onde lente; sopra ci passano eventi rari (tromba da battello, clacson,
  sirena) piazzati ogni volta in un punto diverso della citta'. La sorgente del
  brusio sta **fuori dalla vetrata**, non e' un suono 2D: avvicinandosi al vetro
  cresce e girando la testa si sposta. Un `AudioLowPassFilter` a 900 Hz fa il
  vetro chiuso — e' il taglio degli acuti, piu' del volume, a raccontarlo.
- **Occlusion culling**: bake Umbra limitato da un'`OcclusionArea` sulla stanza,
  cosi' Umbra non voxelizza un chilometro di citta' per punti dove la camera non
  potra' mai stare. Il pannello di vetro e' escluso dagli occluder: e' opaco per
  Umbra e nasconderebbe tutta la citta' proprio quando la guardi.

## Asset di terze parti

Texture PBR da [ambientCG](https://ambientcg.com), licenza **CC0** — vedi
`Assets/Vendor/ambientCG/LICENSE.txt`. Tutto il resto (mesh, texture delle
finestre, cielo, materiali, audio) e' generato da codice.

## Note

- La scena non e' mai stata provata dentro un visore: la verifica si e' fermata
  alla compilazione headless e al rendering da scrivania. Scala percepita dello
  skyline, comfort della locomozione e frame rate vanno controllati alla prima
  prova in VR.
- L'illuminazione e' tutta realtime, nessun lightmap bake: su una GPU desktop
  conviene, e permette di rigenerare la scena in pochi secondi.
