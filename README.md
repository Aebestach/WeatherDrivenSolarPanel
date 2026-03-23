# 🛰️ WeatherDrivenSolarPanel (WDSP)
[English](https://github.com/Aebestach/WeatherDrivenSolarPanel/blob/master/README.md) | [中文](https://github.com/Aebestach/WeatherDrivenSolarPanel/blob/master/README-zh.md)

![Banner](https://i.imgur.com/WoxMQ3K.jpg)

## 📖 Introduction
In stock Kerbal Space Program, solar energy output is determined by sunlight, distance, and occlusions. With blackrack's **[True Volumetric Clouds](https://www.patreon.com/blackrack/posts)**, WDSP adds a new layer of realism: ***Weather***.

Experience dynamic energy fluctuations when encountering rain, snow, dust storms, or volcanic ash on alien worlds.

> ⚠️ **Key Update**: Since v5.0, a **Wear Feature** has been introduced. See [**Others**](#-others) for details.

---

## 🛠️ Dependencies
* **[Kopernicus](https://github.com/Kopernicus/Kopernicus)** —— *[Please use version v212 or later]*
    * *Recommended: Check [this Issue](https://github.com/Aebestach/WeatherDrivenSolarPanel/issues/5) for common setup errors.*
* **[True Volumetric Clouds](https://www.patreon.com/blackrack/posts)** —— Required for weather data.
* **Module Manager** —— Required for patch application.
* **Kerbalism Users**: Ensure `000_Harmony` is installed.

---

## 📥 Installation
1.  **Standard**: Drop the `WeatherDrivenSolarPanel` folder from the `GameData` folder into your game's root `GameData` directory.
2.  **Kerbalism Users**:
    * Copy `Extras\KerbalismSupport\WeatherDrivenSolarPanel.dll` into `GameData\WeatherDrivenSolarPanel\Plugin` and overwrite the existing file.

---

## 📺 Media & Compatibility
| Gallery 1 | Gallery 2 | Gallery 3 |
| :---: | :---: | :---: |
| ![Img1](https://i.imgur.com/B9q2Rak.jpg) | ![Img2](https://i.imgur.com/drHOD4A.jpg) | ![Img3](https://i.imgur.com/oz1DLv0.jpg) |

🎬 **Watch on YouTube**: [Link](https://youtu.be/IKnQO8X81A4?si=3_P_wxlH7WFWAL_2)

* **Fully Compatible**: JNSQ, KSRSS, Kcalbeloh, RO (replaces stock modules), Kerbalism.
* **Requests**: Submit an issue or PR on GitHub for new planet pack support.

---
## ⚠️ Known Issues
- **Energy Fluctuations When Switching Tracking Targets**: When employing trackable solar panels, switching the current tracking target of the controlled vehicle may cause a brief "spike" in energy output. This will normalise once the solar panels are realigned with the new tracking target.

---
## ⚙️ Others

### 🚨 Wear Mechanism (v5.0+)
Panels now suffer from irreversible aging. Once wear reaches **100%**, the panel is permanently disabled and **cannot be repaired** (unless you break it).
* **Time-Based Decay**: Wear increases gradually over time.
* **Weather Impact**: Adverse weather accelerates the wear process.

### 🧬 Kerbalism Logic (v6.0+)
When `switchTimeDecayWear` and `switchWeatherAffectWear` are enabled:
* **Background Processing**: Only **Time Decay** is calculated. Weather-induced wear is ignored to optimize performance.
* **Active/Real-time Processing**: Both **Time** and **Weather** factors are fully simulated.

> [!NOTE]
> **Configuration**: These features are irreversible in-game but can be disabled via `WeatherDrivenSolarPanel/Config/GlobalConfig.cfg`.

#### 📊 Wear Analytics
![Time vs Wear](https://imgur.com/2pyqZmO.png)
![Weather vs Wear](https://imgur.com/7LH2wLB.png)

---

## 🤝 Credits
* Deep gratitude to **[R-T-B](https://github.com/R-T-B)** for technical guidance on WDSP.
* Special thanks to **[blackrack](https://github.com/LGhassen)** for his stunning visual mods and ongoing support.