using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;
using System.Linq;

/// <summary>
/// 菜单：工具 → 打包 Windows64 版本
/// 在 Unity 编辑器里一键产出可交付的 exe，不用敲命令行。
/// </summary>
public static class GameBuilder
{
    const string OutputFolderName = "Win64";
    const string ExeName = "YuansuPlan";

    [MenuItem("工具/打包 Windows64 版本")]
    public static void BuildWindows64()
    {
        // 1. 平台不对就先切过去
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
        {
            bool ok = EditorUtility.DisplayDialog(
                "需要切换平台",
                "当前构建平台不是 Windows 64 位，是否切换到 StandaloneWindows64？",
                "切换", "取消");
            if (!ok) return;
            EditorUserBuildSettings.SwitchActiveBuildTarget(
                BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
        }

        // 2. 输出路径：工程根目录/Deliverable/Win64/YuansuPlan.exe
        string projectRoot = Path.GetDirectoryName(Application.dataPath);   // .../YuansuPlan
        string outDir = Path.Combine(projectRoot, "Deliverable", OutputFolderName);
        string exePath = Path.Combine(outDir, ExeName + ".exe");

        // 3. 场景：取 Build Settings 里勾选启用的场景
        string[] scenes = EditorBuildSettings.scenes
                          .Where(s => s.enabled)
                          .Select(s => s.path)
                          .ToArray();

        if (scenes.Length == 0)
        {
            EditorUtility.DisplayDialog("没有场景",
                "Build Settings 里没有勾选任何场景。\r\n请先打开 SampleScene，然后 Add Open Scenes。",
                "知道了");
            return;
        }

        // 4. 清掉上次的产物，保证目录干净
        if (Directory.Exists(outDir))
        {
            try { Directory.Delete(outDir, true); }
            catch (IOException e)
            {
                Debug.LogWarning("[GameBuilder] 清理旧产物失败：" + e.Message +
                                 "\r\n如果游戏还在运行，先关掉再打包。");
            }
        }
        Directory.CreateDirectory(outDir);

        // 5. 正式构建（不勾 Development Build，交付版不要带调试控制台）
        var opt = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = exePath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        Debug.Log("[GameBuilder] 开始构建，场景：" + string.Join(", ", scenes));
        BuildReport report = BuildPipeline.BuildPlayer(opt);
        BuildSummary sum = report.summary;

        if (sum.result != BuildResult.Succeeded)
        {
            EditorUtility.DisplayDialog("打包失败",
                "构建未成功，请看 Console 里的红色报错。", "好");
            return;
        }

        // 6. 删掉不需要跟着交付的 Burst 调试目录
        foreach (string d in Directory.GetDirectories(outDir, "*_BurstDebugInformation_DoNotShip"))
        {
            try { Directory.Delete(d, true); } catch { }
        }

        // 7. 附一份运行说明进包
        File.WriteAllText(Path.Combine(outDir, "运行说明.txt"), ReadmeText(),
            System.Text.Encoding.UTF8);

        double mb = sum.totalSize / (1024.0 * 1024.0);
        Debug.Log($"[GameBuilder] 构建成功：{exePath}（{mb:F1} MB，用时 {sum.totalTime.TotalSeconds:F1} 秒）");

        EditorUtility.RevealInFinder(exePath);
        EditorUtility.DisplayDialog("打包完成",
            $"已生成 YuansuPlan.exe（{mb:F1} MB，用时 {sum.totalTime.TotalSeconds:F1} 秒）\r\n" +
            $"位置：{outDir}\r\n\r\n" +
            "交付时请把整个 Win64 文件夹压缩成 zip，\r\n不要只发 exe（旁边那个 YuansuPlan_Data 文件夹缺了打不开）。",
            "好");
    }

    static string ReadmeText()
    {
        return
            "元素战机 YuansuPlan —— Windows 64 位\r\n" +
            "\r\n" +
            "【运行】\r\n" +
            "  双击 YuansuPlan.exe 即可，无需安装。\r\n" +
            "  系统要求：Windows 10 / 11，64 位。\r\n" +
            "  注意：本文件夹里的 YuansuPlan_Data 是游戏资源，必须和 exe 放在一起。\r\n" +
            "\r\n" +
            "【操作】\r\n" +
            "  W A S D        移动\r\n" +
            "  方向键         调整射击方向\r\n" +
            "  开火           自动，无需按键\r\n" +
            "  Q / R          切换元素属性（上一个 / 下一个）\r\n" +
            "  F              释放元素大招\r\n" +
            "  1 / 2 / 3      升级时选择卡片\r\n" +
            "  P              暂停\r\n" +
            "  Esc            返回主菜单\r\n" +
            "\r\n" +
            "【关卡】\r\n" +
            "  熔岩裂谷、深海遗迹、雷鸣高塔、岩山废矿\r\n" +
            "  前四关全部通关后解锁「无尽模式」\r\n" +
            "  每关右下角有彩蛋，靠近即可触发\r\n" +
            "\r\n" +
            "【常见问题】\r\n" +
            "  1. 杀毒软件报毒：本程序没有数字签名，属于误报，加白名单即可。\r\n" +
            "  2. 双击没反应：确认是从 zip 里整个解压出来的，不是只拷了 exe。\r\n" +
            "  3. 想重来：删掉 我的文档同级目录下的存档文件即可重置。\r\n" +
            "     存档位置：C:\\Users\\<你的用户名>\\AppData\\LocalLow\\DefaultCompany\\YuansuPlan\r\n" +
            "  4. 中文显示异常：请确认系统已安装中文字体。\r\n";
    }
}
