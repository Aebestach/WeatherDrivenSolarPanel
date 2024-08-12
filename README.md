# WeatherDrivenSolarPanel (WDSP)
[English](https://github.com/Aebestach/WeatherDrivenSolarPanel/blob/master/README.md) | [简体中文](https://www.bilibili.com/read/cv31075491/)

![Imgur](https://imgur.com/WoxMQ3K.jpg)

## Introduce

In the stock version of Kerbal Space Program, the energy output of solar panels is affected by direct sunlight, being blocked by terrain, being blocked by satellites, and being blocked by other parts. With the release of blackrack’s [True Volumetric Clouds](https://www.patreon.com/blackrack/posts), there is now another way to change energy output --- ***weather***. <br><br>This plugin is designed to be used in conjunction with True Volumetric Clouds. When you encounter rain, snow, dust storms, or volcanic clouds on a planet, you will notice a wonderful change in the values on the panel. Enjoy the game to the fullest!

Since the v5.0 update, a new wear feature has been added to WDSP, see [**Others**](https://github.com/Aebestach/WeatherDrivenSolarPanel?tab=readme-ov-file#others) for details

## Dependencies

- [Kopernicus](https://github.com/Kopernicus/Kopernicus)  ***[Please use version v212 or later]***
- [True volumetric clouds](https://www.patreon.com/blackrack/posts)
- [Module Manager](https://forum.kerbalspaceprogram.com/topic/50533-18x-112x-module-manager-423-july-03th-2023-fireworks-season/)

## Installation
- The installation process is the same as other mods. Just put the WeatherDrivenSolarPanel folder from GameData into the GameData in the game root directory. 
- Since ray tracing is now used to calculate EC, for some computers with lower CPU performance, you will need to put the `Extra\LowPerformancePlugin\WeatherDrivenSolarPanel.dll` into the `GameData\WeatherDrivenSolarPanel\Plugin` to overwrite the original *WeatherDrivenSolarPanel.dll*. 
<br>(The default is for high performance computer users. In fact you can try to use the high performance *.dll* and if there is noticeable lag then use the low performance *.dll*).

## Preview Image & Video
![Imgur](https://imgur.com/B9q2Rak.jpg)
![Imgur](https://imgur.com/drHOD4A.jpg)
![Imgur](https://imgur.com/oz1DLv0.jpg)
[YouTube](https://youtu.be/IKnQO8X81A4?si=3_P_wxlH7WFWAL_2) 


## Compatibility
- RSS-Reborn needs ballisticfox to update his Kopernicus
- Incompatible with Kerbalism
- Compatible with JNSQ, KSRSS, Kcalbeloh System, RO (for RO, replaces the original solar module of RO)

## Others 
Since the v5.0 update, the wear feature has been introduced. Once the wear reaches 100%, the solar panel will be damaged and cannot be repaired.
1. The solar panel will increase wear over time.
2. The solar panel will increase wear due to weather.

**Note: This process is irreversible.** If you do not need these two new features, you can turn them off in`WeatherDrivenSolarPanel/Config/GlobalConfig.cfg`.

* * *
**Provides two graphs, one for time versus wear and one for weather versus wear.**

![Imgur](https://imgur.com/vvyXzUw.png)
![Imgur](https://imgur.com/YpfMMHJ.png)

## Credits
[@R-T-B](https://github.com/R-T-B)      Part of the Kopernicus code was used.
<br>[@LGhassen](https://github.com/LGhassen)      Thank him for bringing the commendable volumetric cloud mod to KSP.
<br> **Thanks to both of them for their help with this plugin!**
