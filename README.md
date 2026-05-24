# WeatherDrivenSolarPanel (WDSP)
[English](https://github.com/Aebestach/WeatherDrivenSolarPanel/blob/master/README.md) | [中文](https://github.com/Aebestach/WeatherDrivenSolarPanel/blob/master/README-zh.md)

![Banner](https://i.imgur.com/WoxMQ3K.jpg)

## Introduction
In stock Kerbal Space Program, solar energy output is determined by sunlight, distance, and occlusions. With blackrack's **[True Volumetric Clouds](https://www.patreon.com/blackrack/posts)**, WDSP adds a new layer of realism: ***Weather***.

Experience dynamic energy fluctuations when encountering rain, snow, dust storms, or volcanic ash on alien worlds.

> **Key Update**: Since v5.0, a **Wear Feature** has been introduced. See [**Others**](#others) for details.

---

## Dependencies
* **[True Volumetric Clouds](https://www.patreon.com/blackrack/posts)** —— Required for weather data.
* **Module Manager** —— Required for patch application.

---

## Installation
Drop the `WeatherDrivenSolarPanel` folder from the `GameData` folder into your game's root `GameData` directory.

---

## Media & Compatibility
| Gallery 1 | Gallery 2 | Gallery 3 |
| :---: | :---: | :---: |
| ![Img1](https://i.imgur.com/B9q2Rak.jpg) | ![Img2](https://i.imgur.com/drHOD4A.jpg) | ![Img3](https://i.imgur.com/oz1DLv0.jpg) |

**Watch on YouTube**: [Link](https://youtu.be/IKnQO8X81A4?si=3_P_wxlH7WFWAL_2)

* **Fully Compatible**: JNSQ, KSRSS, Kcalbeloh, RO (replaces stock modules), Kerbalism.
* **Requests**: Submit an issue or PR on GitHub for new planet pack support.

---
## Others

### Wear Mechanism (v5.0+)
Panels now suffer from irreversible aging. Once wear reaches **100%**, the panel is permanently disabled and **cannot be repaired** (unless you break it).
* **Time-Based Decay**: Wear increases gradually over time.
* **Weather Impact**: Adverse weather accelerates the wear process.

### Kerbalism Logic (v6.0+)
When `switchTimeDecayWear` and `switchWeatherAffectWear` are enabled:
* **Background Processing**: Only **Time Decay** is calculated. Weather-induced wear is ignored to optimize performance.
* **Active/Real-time Processing**: Weather output and weather wear are applied automatically when Kerbalism is installed. No separate Kerbalism DLL is required.

> [!NOTE]
> **Configuration**: These features are irreversible in-game but can be disabled via `WeatherDrivenSolarPanel/Config/GlobalConfig.cfg`.

#### Wear Analytics
![Time vs Wear](https://imgur.com/2pyqZmO.png)
![Weather vs Wear](https://imgur.com/7LH2wLB.png)

---

## Credits
* Deep gratitude to **[R-T-B](https://github.com/R-T-B)** for technical guidance on WDSP.
* Special thanks to **[blackrack](https://github.com/LGhassen)** for his stunning visual mods and ongoing support.
