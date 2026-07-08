public partial class MPUser
{
    private static MPUser m_instance;
    private MPUser() { }
    public static MPUser instance
    {
        get
        {
            if (m_instance == null)
            {
                m_instance = new MPUser();
            }

            return m_instance;
        }
    }

    public void Initialization()
    {
        InitAssets();
        InitMainLevel();
        InitCustomLevel();
        // 初始化宠物存档数据，并和当前 pets_config 配置做一次同步。
        InitPets();
    }

    #region Key

    #endregion

    #region Fields

    #endregion

    #region Method

    #endregion
}
