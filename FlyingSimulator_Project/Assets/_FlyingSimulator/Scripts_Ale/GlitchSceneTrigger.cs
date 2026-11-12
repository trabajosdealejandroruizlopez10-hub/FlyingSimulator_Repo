using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;


public class GlitchSceneTrigger : MonoBehaviour
{
    [Header("Destino")]
    [Tooltip("Nombre de la escena. Vacío = siguiente escena en Build Profiles.")]
    public string sceneName = "";

    [Header("Quién lo activa")]
    [Tooltip("Tag que debe tener el objeto que toca. Vacío = cualquiera.")]
    public string requiredTag = "Player";

    [Header("Glitch")]
    public float glitchDuration = 0.7f;
    public bool glitchSound = true;

    bool triggered;

    void OnTriggerEnter(Collider other) { Try(other.gameObject); }
    void OnTriggerEnter2D(Collider2D other) { Try(other.gameObject); }
    void OnCollisionEnter(Collision c) { Try(c.gameObject); }
    void OnCollisionEnter2D(Collision2D c) { Try(c.gameObject); }

    void Try(GameObject go)
    {
        if (triggered) return;
        if (!string.IsNullOrEmpty(requiredTag) && !go.CompareTag(requiredTag)) return;
        triggered = true;
        GlitchTransition.Run(sceneName, glitchDuration, glitchSound);
    }
}

public class GlitchTransition : MonoBehaviour
{
    string targetScene;
    float duration, t;
    bool active, sound;
    Texture2D shot, pixel;

    public static void Run(string scene, float duration, bool sound)
    {
        var go = new GameObject("GlitchTransition");
        DontDestroyOnLoad(go);
        var g = go.AddComponent<GlitchTransition>();
        g.targetScene = scene;
        g.duration = Mathf.Max(0.1f, duration);
        g.sound = sound;
        g.StartCoroutine(g.Routine());
    }

    IEnumerator Routine()
    {
        yield return new WaitForEndOfFrame();
        shot = ScreenCapture.CaptureScreenshotAsTexture();
        pixel = new Texture2D(1, 1);
        pixel.SetPixel(0, 0, Color.white);
        pixel.Apply();

        if (sound) PlayNoise();
        active = true;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        SceneManager.sceneLoaded += OnLoaded;
        if (string.IsNullOrEmpty(targetScene))
        {
            int next = (SceneManager.GetActiveScene().buildIndex + 1) % SceneManager.sceneCountInBuildSettings;
            SceneManager.LoadScene(next);
        }
        else
        {
            SceneManager.LoadScene(targetScene);
        }
    }

    void OnLoaded(Scene s, LoadSceneMode m)
    {
        SceneManager.sceneLoaded -= OnLoaded;
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (shot) Destroy(shot);
        if (pixel) Destroy(pixel);
    }

    void OnGUI()
    {
        if (!active || shot == null || Event.current.type != EventType.Repaint) return;

        GUI.depth = -10000;
        float k = Mathf.Clamp01(t / duration);
        float intensity = Mathf.Lerp(0.15f, 1f, k * k);
        int w = Screen.width, h = Screen.height;

        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(0, 0, w, h), shot);

        int y = 0;
        while (y < h)
        {
            int sh = Random.Range(4, Mathf.Max(8, (int)(h * 0.12f)));
            if (y + sh > h) sh = h - y;

            if (Random.value < intensity * 0.6f)
            {
                float dx = Random.Range(-1f, 1f) * w * 0.2f * intensity;
                var uv = new Rect(0, 1f - (float)(y + sh) / h, 1, (float)sh / h);

                GUI.color = Color.white;
                GUI.DrawTextureWithTexCoords(new Rect(dx, y, w, sh), shot, uv);

                if (Random.value < 0.5f)
                {
                    float split = Random.Range(8f, 40f) * intensity;
                    GUI.color = new Color(1f, 0f, 0.2f, 0.45f);
                    GUI.DrawTextureWithTexCoords(new Rect(dx + split, y, w, sh), shot, uv);
                    GUI.color = new Color(0f, 1f, 1f, 0.45f);
                    GUI.DrawTextureWithTexCoords(new Rect(dx - split, y, w, sh), shot, uv);
                }
            }
            y += sh;
        }

        int blocks = Mathf.RoundToInt(Mathf.Lerp(3, 40, intensity));
        for (int i = 0; i < blocks; i++)
        {
            float bw = Random.Range(w * 0.02f, w * 0.35f);
            float bh = Random.Range(h * 0.005f, h * 0.06f);
            GUI.color = Random.value switch
            {
                < 0.25f => new Color(0f, 1f, 0.3f, 0.8f),
                < 0.5f => new Color(1f, 0f, 0.8f, 0.8f),
                < 0.75f => Color.black,
                _ => Color.white
            };
            GUI.DrawTexture(new Rect(Random.Range(0, w), Random.Range(0, h), bw, bh), pixel);
        }

        if (k > 0.7f && Random.value < 0.25f)
        {
            GUI.color = Random.value < 0.5f ? Color.black : new Color(1f, 1f, 1f, 0.9f);
            GUI.DrawTexture(new Rect(0, 0, w, h), pixel);
        }

        GUI.color = Color.white;
    }

    void PlayNoise()
    {
        int rate = 44100;
        int samples = (int)(rate * duration);
        var data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float env = 1f - (float)i / samples * 0.3f;
            data[i] = (Random.value * 2f - 1f) * 0.25f * env
                    + (Mathf.Sign(Mathf.Sin(i * 0.03f * (1 + (i / 800 % 7)))) * 0.1f);
        }
        var clip = AudioClip.Create("glitch", samples, 1, rate, false);
        clip.SetData(data, 0);
        var src = gameObject.AddComponent<AudioSource>();
        src.spatialBlend = 0f;
        src.PlayOneShot(clip);
    }
}