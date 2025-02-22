﻿using BetterGenshinImpact.Core.Config;
using BetterGenshinImpact.GameTask.GameLoading.Assets;
using System;
using System.Diagnostics;
using BetterGenshinImpact.GameTask.Common;
using BetterGenshinImpact.GameTask.Common.BgiVision;
using BetterGenshinImpact.GameTask.Common.Element.Assets;
using BetterGenshinImpact.GameTask.Model.Area;
using Microsoft.Extensions.Logging;
using BetterGenshinImpact.Genshin.Paths;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Microsoft.Win32;
using System.Windows.Documents;



namespace BetterGenshinImpact.GameTask.GameLoading;

public class GameLoadingTrigger : ITaskTrigger
{
    public string Name => "自动开门";

    public bool IsEnabled { get; set; }

    public int Priority => 999;

    public bool IsExclusive => false;

    public bool IsBackgroundRunning => true;

    private readonly GameLoadingAssets _assets;

    private readonly GenshinStartConfig _config = TaskContext.Instance().Config.GenshinStartConfig;


    // private int _enterGameClickCount = 0;
    // private int _welkinMoonClickCount = 0;
    // private int _noneClickCount, _wmNoneClickCount;

    private DateTime _prevExecuteTime = DateTime.MinValue;

    private DateTime _triggerStartTime = DateTime.Now;

    private string GameServer = "";

    private string channelValue = "";

    private string FileName = "";

    public GameLoadingTrigger()
    {
        GameLoadingAssets.DestroyInstance();
        _assets = GameLoadingAssets.Instance;
    }

    public void Init()
    {
        

        IsEnabled = _config.AutoEnterGameEnabled;

        // // 前面没有联动启动原神，这个任务也不用启动
        // if ((DateTime.Now - TaskContext.Instance().LinkedStartGenshinTime).TotalMinutes >= 5)
        // {
        //     IsEnabled = false;
        // }
        if (_config.RecordGameTimeEnabled)
        {
            
            

            FileName = Path.GetFileName(_config.InstallPath);
            if (FileName == "GenshinImpact.exe") {
                GameServer = "hk4e_global";
                StartStarward();
            }
            if (FileName == "YuanShen.exe")
            {

                string iniPath = Path.GetDirectoryName(_config.InstallPath) + "//config.ini";
                string iniContent;
                string pattern = @"
            ^\s*\[General\]\s*$
            (?:(?!\[).|\r?\n)*
            ^\s*channel=(\S+)
        ";

                try
                {
                    iniContent = File.ReadAllText(iniPath);
                    Regex regex = new Regex(pattern, RegexOptions.Multiline | RegexOptions.IgnorePatternWhitespace | RegexOptions.IgnoreCase);
                    Match match = regex.Match(iniContent);
                    channelValue = match.Success ? match.Groups[1].Value : "";
                }
                catch (Exception e)
                {
                }

                

                


                // channelValue = 1 ： 官服
                // channelValue = 14 ： B服
                if (channelValue == "1")
                {
                    GameServer = "hk4e_cn";
                    StartStarward();
                }
                if (channelValue == "14")
                {
                    GameServer = "hk4e_bilibili";
                    StartStarward();
                }
                


                Debug.WriteLine($"[GameLoading] 从文件读取到游戏区服：{GameServer}");
                // 这里注册表的优先级要比读取文件低，因为使用starward安装原神不会写入注册表
                if (GameServer == null)
                {
                    GameServer = GetGameServerRegistry();
                    Debug.WriteLine($"[GameLoading] 从注册表读取到游戏区服：{GameServer}");
                    StartStarward();
                }
                
            }
        } }
    public bool StartStarward()
    {
        try
        {
            Debug.WriteLine($"[GameLoading] 服务器：{GameServer}");
            if (IsStarwardProtocolRegistered())
            {
                Process.Start(new ProcessStartInfo($"starward://playtime/{GameServer}") { UseShellExecute = true });
                return true;
            }
            else
            {
                TaskControl.Logger.LogWarning("没有检测到 Starward 协议注册，请查看帮助文档！");
                return false;
            }
        }
        catch (Exception ex)
        {


            Debug.WriteLine("[GameLoading] Starward记录时间失败");
            return false;
        }
    }
    public string GetGameServerRegistry()
    {
        try
        {
            var cn = Registry.GetValue($@"HKEY_CURRENT_USER\Software\miHoYo\HYP\1_1\hk4e_cn", "GameInstallPath", null) as string;
            if (!string.IsNullOrEmpty(cn))
            {
                var filePath = Path.Combine(cn, "YuanShen.exe");
                GameServer = "hk4e_cn";
                return GameServer;
            }

            var global = Registry.GetValue($@"HKEY_CURRENT_USER\Software\Cognosphere\HYP\1_0\hk4e_global", "GameInstallPath", null) as string;
            if (!string.IsNullOrEmpty(global))
            {
                var filePath = Path.Combine(global, "GenshinImpact.exe");
                GameServer = "hk4e_global";
                return GameServer;
            }

            var bilibili = Registry.GetValue($@"HKEY_CURRENT_USER\Software\miHoYo\HYP\standalone\14_0\hk4e_cn\umfgRO5gh5\hk4e_cn", "GameInstallPath", null) as string;
            if (!string.IsNullOrEmpty(bilibili))
            {
                var filePath = Path.Combine(bilibili, "YuanShen.exe");
                GameServer = "hk4e_bilibili";
                return GameServer;

            }
        }
        catch (Exception e)
        {
            TaskControl.Logger.LogDebug(e, "获取服务器失败");
        }
        return "";
    }
    public bool IsStarwardProtocolRegistered()
    {
        try
        {
            // 打开注册表路径 HKEY_CLASSES_ROOT\starward
            using (RegistryKey key = Registry.ClassesRoot.OpenSubKey("starward"))
            {
                // 如果键存在
                if (key != null)
                {
                    // 检查是否存在 URL Protocol 值
                    object urlProtocol = key.GetValue("URL Protocol");
                    // 如果 URL Protocol 存在且值为空字符串（标准配置），认为协议已注册
                    if (urlProtocol != null && urlProtocol.ToString() == "")
                    {
                        return true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // 如果访问注册表时发生错误，记录调试信息
            Debug.WriteLine($"[GameLoading] 检查 Starward 协议时发生错误: {ex.Message}");
        }
        // 如果键不存在或不符合条件，返回 false
        return false;
    }
    public void OnCapture(CaptureContent content)
    {
        // 2s 一次
        if ((DateTime.Now - _prevExecuteTime).TotalMilliseconds <= 2000)
        {
            return;
        }
        _prevExecuteTime = DateTime.Now;
        // 5min 后自动停止
        if ((DateTime.Now - _triggerStartTime).TotalMinutes >= 5)
        {
            IsEnabled = false;
            return;
        }

        if (Bv.IsInMainUi(content.CaptureRectArea) || Bv.IsInAnyClosableUi(content.CaptureRectArea))
        {
            IsEnabled = false;
            return;
        }

        using var ra = content.CaptureRectArea.Find(_assets.EnterGameRo);
        if (!ra.IsEmpty())
        {
            // 随便找个相对点击的位置
            TaskContext.Instance().PostMessageSimulator.LeftButtonClickBackground();
            // TaskControl.Logger.LogInformation("自动开门");
            return;
        }

        var wmRa = content.CaptureRectArea.Find(_assets.WelkinMoonRo);
        if (!wmRa.IsEmpty())
        {
            TaskContext.Instance().PostMessageSimulator.LeftButtonClickBackground();
            Debug.WriteLine("[GameLoading] Click blessing of the welkin moon");
            // TaskControl.Logger.LogInformation("自动点击月卡");
            return;
        }

        // 原石
        var ysRa = content.CaptureRectArea.Find(ElementAssets.Instance.PrimogemRo);
        if (!ysRa.IsEmpty())
        {
            GameCaptureRegion.GameRegion1080PPosMove(10, 10);
            TaskContext.Instance().PostMessageSimulator.LeftButtonClickBackground();
            Debug.WriteLine("[GameLoading] 跳过原石");
            return;
        }
    }
};