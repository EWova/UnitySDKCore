using UnityEngine;

public class Test : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        print(EWova.EWova.GetDeepLink());
        print(EWova.EWova.GetDeepLink(EWova.DeepLinkQueryInclude.Default));
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
