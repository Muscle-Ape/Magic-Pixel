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
        m_isInitializingUserData = true;
        try
        {
            InitSetting();
            InitAssets();
            InitMainLevel();
            InitLargeImageLevel();
            InitCustomLevel();
            InitPets();
        }
        finally
        {
            m_isInitializingUserData = false;
        }
    }
}
