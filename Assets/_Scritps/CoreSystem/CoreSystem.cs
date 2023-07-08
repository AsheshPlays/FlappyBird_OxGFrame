﻿using Cysharp.Threading.Tasks;
using OxGFrame.MediaFrame;
using OxGKit.Utilities.Singleton;
using UnityEngine;

public class CoreSystem : MonoSingleton<CoreSystem>
{
    public static float deltaTime = 0f;

    [Range(24, 60)]
    public int frameRate = 30;

    private static int _currentScore;

    private void Awake()
    {
        // Init instance first
        GetInstance();
    }

    private void Start()
    {
        // 30 for Mobile, 60 for Desktop
        Application.targetFrameRate = this.frameRate;

        GSIManager.GetInstance().OnStart();
    }

    private void Update()
    {
        deltaTime = Time.deltaTime;

        GSIManager.GetInstance().OnUpdate(deltaTime);
    }

    #region Score Controls
    /// <summary>
    /// 重置分數
    /// </summary>
    public static void ResetScore()
    {
        // 歸零
        _currentScore = 0;
    }

    /// <summary>
    /// 增加分數
    /// </summary>
    public static void AddScore()
    {
        // 分數 + 1
        _currentScore++;

        // 播放增加分數音效
        MediaFrames.AudioFrame.Play(Audios.ScoreSfx).Forget();
    }

    /// <summary>
    /// 取得分數
    /// </summary>
    /// <returns></returns>
    public static int GetScore()
    {
        return _currentScore;
    }

    /// <summary>
    /// 記錄最佳分數
    /// </summary>
    /// <param name="score"></param>
    public static void SaveBestScore()
    {
        // 判斷分數如果有 > 最佳分數, 才進行記錄
        if (GetScore() > GetBestScore()) PlayerPrefs.SetInt("BestScore", GetScore());
    }

    /// <summary>
    /// 取得最佳分數
    /// </summary>
    /// <returns></returns>
    public static int GetBestScore()
    {
        return PlayerPrefs.GetInt("BestScore", 0);
    }
    #endregion

    #region Game Stage Controls
    /// <summary>
    /// [Global] 遊戲是否開始
    /// </summary>
    /// <returns></returns>
    public static bool IsGameStart()
    {
        return GSIManager.GetInstance().GetGameStage<GamePlayStage>().IsGameStart();
    }

    /// <summary>
    /// 前往主選單階段
    /// </summary>
    public static void GoToMenu()
    {
        // 切換至 MainMenuStage
        GSIManager.GetInstance().ChangeGameStage<MainMenuStage>();
    }

    /// <summary>
    /// 進入遊戲
    /// </summary>
    public static void EnterGame()
    {
        // 切換 MainMenuStage 步驟
        GSIManager.GetInstance().GetGameStage<MainMenuStage>().ChangeStep(MainMenuStage.MainMenuStep.START_GAME_PLAY);
    }

    /// <summary>
    /// 開始遊戲
    /// </summary>
    public static void StartGame()
    {
        // 切換 GamingStage 步驟
        GSIManager.GetInstance().GetGameStage<GamePlayStage>().ChangeStep(GamePlayStage.GamePlayStep.START_GAME);
    }

    /// <summary>
    /// 遊戲結束
    /// </summary>
    public static void GameOver()
    {
        // 切換 GamingStage 步驟
        GSIManager.GetInstance().GetGameStage<GamePlayStage>().ChangeStep(GamePlayStage.GamePlayStep.GAMEOVER);
    }

    /// <summary>
    /// 重新遊玩 (實際上就是強制重新切換一次 GamePlayStage)
    /// </summary>
    public static void Replay()
    {
        GSIManager.GetInstance().ChangeGameStageForce<GamePlayStage>();
    }
    #endregion
}
