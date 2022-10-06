return {
	Title = "蛐蛐透视",
	Description = "蛐蛐透视MOD \n功能简单稳定 \n请按叫声捕捉（因为茄子的恶意，促织王可能变蝈蝈）\n针对评论反馈抓不到的问题已添加强抓功能 \n强抓默认关闭，手动开启即可 \n代码开源地址: \nhttps://github.com/XeonSatan/Taiwu_CricketHacker.git",
	Cover = "Cover.png",
	WorkshopCover = "Cover_wide.png",
	Source = 1,
	FileId = 2871637284,
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
		[2] = {
			Key = "SetSingFlag",
			DisplayName = "开启强抓",
			SettingType = "Toggle",
			DefaultValue = false,
		},
	},
	TagList =
	{
		[1] = "显示-Display",
		[2] = "扩展-Extensions",
	}
}