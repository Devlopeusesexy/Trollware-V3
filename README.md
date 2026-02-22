# 💀 TROLLWARE V3 : The Ultimate System Dominator 💀

![Trollware Banner](https://img.shields.io/badge/Status-Cursed-red) ![Language](https://img.shields.io/badge/C%23-WPF%20%2B%20SkiaSharp-blueviolet) ![Native](https://img.shields.io/badge/C%2B%2B-Syscalls-black)

**Trollware V3** est un framework expérimental de nuisance système conçu pour tester les limites du stress utilisateur et de la manipulation du système d'exploitation Windows. 
Déployable avec `0 Warning, 0 Error`, il s'agit d'une application hybride combinant des hooks bas-niveau C++ (Dll) pour le blocage d'inputs hardware et un moteur de rendu graphique 2D (`SkiaSharp`) surpuissant couplé à WPF.

> ⚠️ **DISCLAIMER :** Ce projet est à but purement éducatif (Reverse Engineering, MalDev, Red Teaming). Ne pas exécuter sur une machine hôte de production. L'auteur ne prend aucune responsabilité pour vos tympans ou vos SSD. 

---

## 💻 Features Principales

### 🔴 1. Domination Système Complète (C++ Native)
Le composant `TrollNative.dll` hook le système pour une domination totale de l'utilisateur :
- **Input Blocking (SetWindowsHookEx) :** Désactivation matérielle absolue de la souris, du clic (L/R/M), de la molette et du clavier (sauf raccourci d'urgence). L'utilisateur est physiquement impuissant face à ce qui se passe à l'écran.
- **Remplacement de Curseur :** Force un custom curseur agaçant (ex: _vtuber animé_) à l'échelle de l'OS via `SetSystemCursor`, impossible à modifier dans les réglages tant que le processus tourne.
- **Volume Forcé :** Spam ininterrompu de l'event clavier `VK_VOLUME_UP` pour garantir 100% de volume audio (bypass de l'UI Windows).
- **Kill Switch d'urgence :** `Ctrl + Alt + F12` est l'unique bouée de sauvetage matérielle non hookée. Elle restaure les inputs, kill le renderer et réinitialise le curseur.

### 🎨 2. Moteur de Rendu Custom (C# SkiaSharp)
L'UI WPF passe en `WindowStyle="None" AllowsTransparency="True" Topmost="True"`, transformant votre écran en un canvas de torture invisible.
La méthode graphique via `SKCanvas` génère des SVGs dessinés manuellement et des traitements GPU asynchrones (60 FPS) :
- **Alpha Blending & Fade-Ins :** Les éléments graphiques insoutenables apparaissent et disparaissent de l'écran via des fonctions Mathématiques (`Math.Sin()`), créant un malaise "Smooth" sans coupure abrupte.
- **Formes Procédurales :** Croix gammées géantes, Phallus et symbologies polémiques générées de zéro en `SKPath`, tournoyant à l'écran sans impacter la RAM.

### 🎮 3. Le Menu "GitHub Ready" (4 Modes)
Le panel d'accueil V3 impose 4 choix mortels :
1. **🐱 RIRIMIAOU (Crescendo) :** Angoisse lente et pression psychologique. Au fil des phases, ce mode fait popper des images incongrues (Brawlstars, Ririmiaou) et des objets explicites avec de violentes secousses d'écran (Screen Shake 100px) et des inversions de couleurs stroboscopiques.
2. **💀 PUNISHER (Extrême) :** Le chaos total. Assaut auditif (`kys.wav`), clignotement rouge/noir épileptique, spam textuel injurieux (insultes racistes, etc.), SVGs tournoyants nazis/homophobes pour une surcharge cognitive instantanée.
3. **🤡 SOFT / JOKES :** Une approche pastel et narquoise de la nuisance. L'écran devient rose/lavande, de doux Svgs obscènes tournent au ralenti, et des insultes légères/blagues sont affichées doucement ("T'es gay ?").
4. **⚙️ DEBUG SVGs :** Conçu pour le développement. Débloque la souris et freeze l'écran en un panel gris sombre affichant côte à côte nos 4 magnifiques `SKPath` mathématiques (SVGs) pour inspection.

---

## 🛠 Compilation (0 Warn / 0 Err Doctrine)
Ce projet applique une politique stricte `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`. L'activation du *Nullable Context Analyzer* assure l'absence de null-dereference.

1. Construire la DLL Native C++ (x64)
2. `dotnet build Trollware.sln`
3. Copier `TrollNative.dll` dans `\bin\Debug\net10.0-windows\`
4. Optionnel : Lancer via `LaunchGame.vbs` pour un stealth startup.

## 🔑 Raccourci Vital
**`CTRL + ALT + F12`** : Restaure le système. Ne l'oubliez pas.
