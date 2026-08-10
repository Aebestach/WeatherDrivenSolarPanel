# WeatherDrivenSolarPanel (WDSP)
[English](https://github.com/Aebestach/WeatherDrivenSolarPanel/blob/master/README.md) | [中文](https://github.com/Aebestach/WeatherDrivenSolarPanel/blob/master/README-zh.md)

![Banner](https://i.imgur.com/WoxMQ3K.jpg)

## 模组简介 (Introduction)
在坎巴拉太空计划的原版中，太阳能板的能量输出仅受光照、地形/卫星遮挡及零件遮蔽的影响。
随着blackrack的 **[True Volumetric Clouds (体积云)](https://www.patreon.com/blackrack/posts)** 问世，我们迎来了影响能源产出的新维度 —— **天气**。

当你的飞船遭遇阴云、降雨、降雪、沙尘暴或火山灰时，WDSP 会实时削弱太阳能板发电；长期暴露还会造成不可逆损耗，沙尘与火山灰还会在板面留下可见积灰。难度设置中可开关各项效果并调节损耗曲线，让能源管理成为更真实的生存挑战。

---

## 功能特性 (Features)

### 损耗系统
太阳能板会产生**不可逆**的损耗。当磨损度达到 **100%** 时，零件将永久损坏且**无法修复**（除非你破坏它）。
* **时间性损耗**：随部署时间推移自然老化。默认：约 **30** Kerbin 年剩一半，约 **50** 年归零。
* **天气性损耗**：降水、沙尘暴、火山灰会加速磨损。默认：暴露约 **2** 年剩一半，约 **5** 年归零。多云层会削弱发电，但**不产生**天气磨损。
* **叠加方式**：两者均开启时，最终效率按 **时间损耗 × 天气损耗** 计算。

### 积灰视觉
已加载的太阳能板会使用同 mesh 的透明 overlay 显示表面积灰：
* 积灰只在沙尘暴 / 火山灰层中累加；有独立暴露计时（降水会损耗，不会积灰）。默认：约 **1.0** Kerbin 年半覆盖，约 **2.5** 年全覆盖。
* 覆盖零件上**所有几何 mesh**（板面、臂杆、铰链等）；主表面满强度，结构更淡（约 **55%**）；特效/工具 mesh 与过细杆件会跳过。
* 原零件材质不会被替换；可在难度设置中关闭积灰视觉。开启后 PAW 始终显示积灰百分比（含舱外查看）。
* **3 级及以上工程师**可在舱外清除积灰：磨损低于 **80%** 时可操作；会移除沙尘/火山灰造成的损耗，但保留降水等损耗。

### Kerbalism 适配
在开启时间性损耗与天气性损耗后：
* **后台资源处理**：仅计算 **时间性损耗**；为优化性能，此时忽略天气对磨损的影响。
* **非后台 / 实时处理**：自动应用天气发电影响与天气磨损，无需单独的 Kerbalism 专用 DLL。

---

## 必要前置 (Dependencies)
* **[True Volumetric Clouds](https://www.patreon.com/blackrack/posts)** —— 天气系统核心。
* **Module Manager** —— 模组配置必需。

---

## 安装指南 (Installation)
将压缩包内 `GameData` 下的 `WeatherDrivenSolarPanel` 文件夹放入游戏根目录的 `GameData` 中即可。

---

## 预览与兼容性 (Preview & Compatibility)
<table width="100%">
  <tr>
    <th width="50%" align="center">预览图 1</th>
    <th width="50%" align="center">预览图 2</th>
  </tr>
  <tr>
    <td width="50%" align="center"><img src="https://i.imgur.com/B9q2Rak.jpg" width="100%" alt="预览图 1" /></td>
    <td width="50%" align="center"><img src="https://i.imgur.com/drHOD4A.jpg" width="100%" alt="预览图 2" /></td>
  </tr>
  <tr>
    <th width="50%" align="center">预览图 3</th>
    <th width="50%" align="center">预览图 4</th>
  </tr>
  <tr>
    <td width="50%" align="center"><img src="https://i.imgur.com/oz1DLv0.jpg" width="100%" alt="预览图 3" /></td>
    <td width="50%" align="center"><img src="https://i.imgur.com/V1DRtjG.jpg" width="100%" alt="预览图 4" /></td>
  </tr>
</table>

**视频演示**：[YouTube 链接](https://youtu.be/IKnQO8X81A4?si=3_P_wxlH7WFWAL_2)

* **完全兼容**：JNSQ, KSRSS, Kcalbeloh, SPVE, RO (替换原版模块), Kerbalism。
* **兼容请求**：若需支持其他行星包，欢迎在 GitHub 提交 Issue 或 PR。

---

## 致谢 (Credits)
* 感谢 **[R-T-B](https://github.com/R-T-B)** 对 WDSP 开发的全程协助。
* 感谢 **[blackrack](https://github.com/LGhassen)** 带来绝美的视觉模组以及在开发中的指导。
