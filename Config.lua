return {
	Title = "蛐蛐透视",
	Version = 1,
	Description = "蛐蛐透视MOD \n功能简单稳定 \n请按叫声捕捉 \n捕捉的蛐蛐有概率变鸣虫（茄子的恶意）\n开源地址: \nhttps://github.com/XeonSatan/Taiwu_CricketHacker.git",
	Cover = "Cover.png",
	HasArchive = false,
	Author = "XeonSatan",
	FrontendPlugins = 
	{
		[1] = "Taiwu_CricketHacker.dll"
	},
	BackendPlugins = 
	{
	},
	DefaultSettings = {
		[1] = {
			Key = "EnableFlag",
			DisplayName = "是否启用",
			SettingType = "Toggle",
			DefaultValue = true,
		},
	}
}