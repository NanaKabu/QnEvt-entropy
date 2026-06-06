# 公开发布与隐私检查清单

这个仓库是为最小公开基线准备的。推送到公开远端前，请检查下面这些内容。

## 已公开

- Pico 2 最小接线说明
- Pico SDK 最小测距固件
- 第三方 VL53L1X C API，并保留上游许可证
- 公开构建说明
- AGPL-3.0 许可证

## 不公开

- 个人笔记或本地 TODO 文件
- 私有校准值
- 私有熵生成或熵调理算法
- 产品特定阈值或尚未发布的决策逻辑
- 设备序列号或库存记录
- Wi-Fi 名称、密码、API key、token 或服务端地址
- 原始采集数据、日志、数据集或生成的二进制文件
- 私有板卡文件或供应商专用资料

## 推送前检查

运行：

```powershell
git status --short
rg -n "password|passwd|secret|token|api[_-]?key|ssid|wifi|private|credential|bearer|http://|https://" .
```

逐条人工确认搜索结果。公开文档中可以把这些词作为说明示例，但仓库里不应该出现真实值。
