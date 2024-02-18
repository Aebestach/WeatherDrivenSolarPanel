# WeatherDrivenSolarPanel (WDSP)
[English](https://github.com/Aebestach/WeatherDrivenSolarPanel/blob/master/README.md) | [简体中文](https://www.bilibili.com/read/cv31075491/)

![Imgur](https://imgur.com/WoxMQ3K.jpg)

## Introduce

In the stock version of Kerbal Space Program, the energy output of solar panels is affected by direct sunlight, being blocked by terrain, being blocked by satellites, and being blocked by other parts. With the release of blackrack’s [True Volumetric Clouds](https://www.patreon.com/blackrack/posts), there is now another way to change energy output --- ***weather***. <br><br>This plugin is designed to be used in conjunction with True Volumetric Clouds. When you encounter rain, snow, dust storms, or volcanic clouds on a planet, you will notice a wonderful change in the values on the panel. Enjoy the game to the fullest!


## Dependencies

- [Kopernicus](https://github.com/Kopernicus/Kopernicus)
- [True volumetric clouds](https://www.patreon.com/blackrack/posts)
- [Module Manager](https://forum.kerbalspaceprogram.com/topic/50533-18x-112x-module-manager-423-july-03th-2023-fireworks-season/)

## Installation
- The installation process is the same as other mods. Just put the WeatherDrivenSolarPanel folder from GameData into the GameData in the game root directory. 
- Since ray tracing is now used to calculate EC, for some computers with lower CPU performance, you will need to put the `Extra\LowPerformancePlugin\WeatherDrivenSolarPanel.dll` into the `GameData\WeatherDrivenSolarPanel\Plugin` to overwrite the original *WeatherDrivenSolarPanel.dll*. 
<br>(The default is for high performance computer users. In fact you can try to use the high performance *.dll* and if there is noticeable lag then use the low performance *.dll*).

## Preview Image & Video
![Imgur](https://imgur.com/WsDzsv7.jpg)
[YouTube](https://youtu.be/IKnQO8X81A4?si=3_P_wxlH7WFWAL_2) 

## Warning
- Unknown at this time

## Compatibility
- Adaptation of RSS-Reborn+RO and RSS-Origin+RO has been completed.
- Adaptation kerbalism, but note that this abandons the EC calculations using kerbalism (which doesn't seem to have much of an impact either).
In fact it's more recommended to use [FuseboxContinued](https://forum.kerbalspaceprogram.com/topic/157896-112x-fusebox-continued-electric-charge-tracker-and-build-helper/), and I've mentioned in the [kerbalism issue](https://github.com/Kerbalism/Kerbalism/issues/886) that the EC calculation in question is wrong.
- The four curved solar panels in the NFSolar are not compatible with this plug-in.
- No other incompatibilities have been received yet.

## Others 
- Due to the inscrutable calculation method of the stock version of EC resources, I am directly using the calculation method under the multi-star mode of Kopernicus here, which is currently the best method I can take.

## Credits
[@R-T-B](https://github.com/R-T-B)      Part of the Kopernicus code was used.
<br>[@LGhassen](https://github.com/LGhassen)      Thank him for bringing the commendable volumetric cloud mod to KSP.
<br> **Thanks to both of them for their help with this plugin!**
