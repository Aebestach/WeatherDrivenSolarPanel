# WeatherDrivenSolarPanel (WDSP)
[English](https://github.com/Aebestach/WeatherDrivenSolarPanel/blob/master/README.md) | [中文](https://github.com/Aebestach/WeatherDrivenSolarPanel/blob/master/README-zh.md)

![Banner](https://i.imgur.com/WoxMQ3K.jpg)

## Introduction
In stock Kerbal Space Program, solar energy output is determined by sunlight, distance, and occlusions. With blackrack's **[True Volumetric Clouds](https://www.patreon.com/blackrack/posts)**, WDSP adds a new layer of realism: ***Weather***.

Clouds, rain, snow, dust storms, and volcanic ash can cut solar output in real time. Prolonged exposure also causes irreversible wear, while dust and ash leave a visible film on panel surfaces. Toggle each effect and tune wear curves in Difficulty Settings for a tougher, more grounded power challenge.

---

## Features

### Wear system
Solar panels suffer **irreversible** wear. Once wear reaches **100%**, the panel is permanently disabled and **cannot be repaired** (unless you break it).
* **Time-based wear**: Panels age while deployed. Default: half output ~**30** Kerbin years, zero ~**50**.
* **Weather-based wear**: Rain, dust storms, and volcanic ash accelerate wear. Default: half ~**2** years of exposure, zero ~**5**. Cloudy layers cut power but do **not** add wear.
* **Combined**: Final efficiency is **time wear × weather wear** when both are enabled.

### Dust visuals
Loaded solar panels use a transparent overlay on the same mesh:
* Dust accumulates only under dust-storm / volcano layers, with its own exposure timer (rain can wear panels without adding dust). Default: half covered ~**1.0** Kerbin year, fully covered ~**2.5**.
* Covers **all geometry meshes** on the part (cells, booms, hinges); primary faces at full strength, structure dimmer (~**55%**). FX / utility meshes and hair-thin rods are skipped.
* Original part materials are left untouched. Dust visuals can be disabled in Difficulty Settings. With dust visuals on, PAW shows dust % (including on EVA).
* **Level 3+ engineers** can clean dust on EVA when wear is below **80%**. Cleaning removes dust/ash wear but keeps precipitation wear.

### Kerbalism support
When time-based and weather-based wear are enabled:
* **Background processing**: Only **time-based wear** is applied; weather wear is skipped for performance.
* **Active / real-time processing**: Weather power and weather wear are applied automatically. No separate Kerbalism DLL is required.

---

## Dependencies
* **[True Volumetric Clouds](https://www.patreon.com/blackrack/posts)** —— Required for weather data.
* **Module Manager** —— Required for patch application.

---

## Installation
Drop the `WeatherDrivenSolarPanel` folder from the `GameData` folder into your game's root `GameData` directory.

---

## Media & Compatibility
<table width="100%">
  <tr>
    <th width="50%" align="center">Gallery 1</th>
    <th width="50%" align="center">Gallery 2</th>
  </tr>
  <tr>
    <td width="50%" align="center"><img src="https://i.imgur.com/B9q2Rak.jpg" width="100%" alt="Gallery 1" /></td>
    <td width="50%" align="center"><img src="https://i.imgur.com/drHOD4A.jpg" width="100%" alt="Gallery 2" /></td>
  </tr>
  <tr>
    <th width="50%" align="center">Gallery 3</th>
    <th width="50%" align="center">Gallery 4</th>
  </tr>
  <tr>
    <td width="50%" align="center"><img src="https://i.imgur.com/oz1DLv0.jpg" width="100%" alt="Gallery 3" /></td>
    <td width="50%" align="center"><img src="https://i.imgur.com/V1DRtjG.jpg" width="100%" alt="Gallery 4" /></td>
  </tr>
</table>

**Watch on YouTube**: [Link](https://youtu.be/IKnQO8X81A4?si=3_P_wxlH7WFWAL_2)

* **Fully Compatible**: JNSQ, KSRSS, Kcalbeloh, RO (replaces stock modules), Kerbalism.
* **Requests**: Submit an issue or PR on GitHub for new planet pack support.

---

## Credits
* Deep gratitude to **[R-T-B](https://github.com/R-T-B)** for technical guidance on WDSP.
* Special thanks to **[blackrack](https://github.com/LGhassen)** for his stunning visual mods and ongoing support.
