namespace GamePlay.Session
{
    /// <summary>局内玩法模式。只在一次对局内存在，随 GameplayRoot 的对局生命周期创建与销毁。</summary>
    public enum GameplayMode
    {
        FreeRoam = 0,
        Dialogue = 1,
        Cutscene = 2,
        Battle = 3,
        Pause = 4,
        GameOver = 5,
        Shop = 6,
    }
}