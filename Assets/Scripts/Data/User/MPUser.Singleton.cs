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
    }

    #region Key

    #endregion

    #region Fields

    #endregion

    #region Method

    #endregion
}
