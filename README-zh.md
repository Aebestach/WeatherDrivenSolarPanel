# WeatherDrivenSolarPanel (WDSP)
[English](https://github.com/Aebestach/WeatherDrivenSolarPanel/blob/master/README.md) | [中文](https://github.com/Aebestach/WeatherDrivenSolarPanel/blob/master/README-zh.md)

![Banner](https://i.imgur.com/WoxMQ3K.jpg)

## 模组简介 (Introduction)
在坎巴拉太空计划的原版中，太阳能板的能量输出仅受光照、地形/卫星遮挡及零件遮蔽的影响。
随着blackrack的 **[True Volumetric Clouds (体积云)](https://www.patreon.com/blackrack/posts)** 问世，我们迎来了影响能源产出的新维度 —— **天气**。

当你的飞船遭遇降雨、降雪、沙尘暴或火山云时，WDSP 将实时改变太阳能板的输出数值，为你带来更真实的深空生存体验。

> **重要更新**：自 v5.0 起，模组引入了 **磨损系统 (Wear Feature)**。详情请参阅 [其他说明](#其他说明-others)。

---

## 必要前置 (Dependencies)
* **[True Volumetric Clouds](https://www.patreon.com/blackrack/posts)** —— 天气系统核心。
* **Module Manager** —— 模组配置必需。

---

## 安装指南 (Installation)
将压缩包内 `GameData` 下的 `WeatherDrivenSolarPanel` 文件夹放入游戏根目录的 `GameData` 中即可。

---

## 预览与兼容性 (Preview & Compatibility)
| 预览图 1 | 预览图 2 | 预览图 3 |
| :---: | :---: | :---: |
| ![Img1](https://i.imgur.com/B9q2Rak.jpg) | ![Img2](https://i.imgur.com/drHOD4A.jpg) | ![Img3](https://i.imgur.com/oz1DLv0.jpg) |

**视频演示**：[YouTube 链接](https://youtu.be/IKnQO8X81A4?si=3_P_wxlH7WFWAL_2)

* **完全兼容**：JNSQ, KSRSS, Kcalbeloh, RO (替换原版模块), Kerbalism。
* **兼容请求**：若需支持其他行星包，欢迎在 GitHub 提交 Issue 或 PR。

---
## 其他说明 (Others)

### 磨损机制 (Wear System)
从 v5.0 版本开始，太阳能板将产生**不可逆**的损耗。当磨损度达到 **100%** 时，零件将永久损坏且**无法修复**（除非你破坏它）。
* **时间性损耗**：随部署时间推移自然老化。
* **环境性损耗**：由恶劣天气导致的加速磨损。

### Kerbalism 适配逻辑 (v6.0+)
在开启 `switchTimeDecayWear` 与 `switchWeatherAffectWear` 后：
* **后台资源处理**：仅计算 **时间性损耗**。为优化性能，此时**忽略**天气对磨损的影响。
* **非后台/实时处理**：安装 Kerbalism 时会自动应用天气发电影响与天气磨损，不再需要单独替换 Kerbalism 专用 DLL。

> [!TIP]
> **提示**：如需关闭此功能，请修改 `WeatherDrivenSolarPanel/Config/GlobalConfig.cfg`。

#### 损耗参考曲线
![Time vs Wear](https://imgur.com/2pyqZmO.png)
![Weather vs Wear](https://imgur.com/7LH2wLB.png)

---

## 致谢 (Credits)
* 感谢 **[R-T-B](https://github.com/R-T-B)** 对 WDSP 开发的全程协助。
* 感谢 **[blackrack](https://github.com/LGhassen)** 带来绝美的视觉模组以及在开发中的指导。
