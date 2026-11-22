using System.Collections.Generic;

[System.Serializable]
public class PageLinkStates
{
    public string pageName;
    public List<string> visitedLinkTargets = new List<string>();
}

[System.Serializable]
public class SaveData
{
    public string currentPageName;
    public List<string> visitedPages = new List<string>();
    public float masterVolume = 1f;
    public bool isFullscreen = true;
    public List<PageLinkStates> pageLinkStates = new List<PageLinkStates>();
}