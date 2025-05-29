using UnityEngine;

public class TextureProvider : MonoBehaviour
{
    //Add Texture PNG here for it to work with Automatic1111
    public Texture2D sourceTexture;
    public Texture2D GetTexture()
    {return sourceTexture;}
}
