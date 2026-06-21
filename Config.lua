return {
	Title = "蛐蛐透视",
	Description = "蛐蛐透视MOD-已适配正式版\n\n增加了蛐蛐决斗透视对手上场蛐蛐功能，可手动开关该功能。\n增加了抓蛐蛐透视的独立开关。\n\n抓蛐蛐时促织不叫的时候默认是抓不到的，可以开启强抓功能无视叫声抓取。\n\n-------------------------------------\n代码开源地址: \nhttps://github.com/XeonSatan/Taiwu_CricketHacker.git",
	Cover = "Cover.png",
	WorkshopCover = "Cover.png",
	Source = 1,
	FileId = 2871637284,
	HasArchive = false,
	Author = "XeonSatan",
	FrontendPlugins = {
		[1] = "Taiwu_CricketHacker.dll",
	},
	DefaultSettings = {
		[1] = {
			SettingType = "Toggle",
			Key = "EnableFlag",
			DisplayName = "是否启用",
			Description = "所有功能的总开关",
			GroupName = "Default",
			DefaultValue = true,
		},
		[2] = {
			SettingType = "Toggle",
			Key = "SetSingFlag",
			DisplayName = "开启强抓",
			Description = "无视蛐蛐是否在叫，直接开抓",
			GroupName = "Default",
			DefaultValue = false,
		},
		[3] = {
			SettingType = "Toggle",
			Key = "EnableCatchCricketFlag",
			DisplayName = "开启抓蛐蛐透视",
			Description = "抓蛐蛐透视功能开关",
			GroupName = "Default",
			DefaultValue = true,
		},
		[4] = {
			SettingType = "Toggle",
			Key = "EnableCombatFlag",
			DisplayName = "开启对战透视",
			Description = "显示蛐蛐决斗时对方隐藏的蛐蛐",
			GroupName = "Default",
			DefaultValue = true,
		},
	},
	TagList = {
		[1] = "Modifications",
		[2] = "Extensions",
		[3] = "Compatible Mods",
	},
	Version = "2.0.0.2",
	GameVersion = "1.0.13",
	Visibility = 0,
	UpdateLogList = {
		[1] = {
			Timestamp = 1708784701,
			LogList = {
				[1] = "更新版本号，不在新版本出现警告",
			},
		},
		[2] = {
			Timestamp = 1708785916,
			LogList = {
				[1] = "..",
			},
		},
		[3] = {
			Timestamp = 1713986784,
			LogList = {
				[1] = "适配新版本号，去警告",
			},
		},
		[4] = {
			Timestamp = 1781700864,
		},
		[5] = {
			Timestamp = 1782057667,
		},
	},
	ChangeConfig = false,
	NeedRestartWhenSettingChanged = false,
	SettingGroups = {
		[1] = "Default",
	},
}
