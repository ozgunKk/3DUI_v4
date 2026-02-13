using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class BoardSlideDeck : MonoBehaviour
{
    [Header("Board Target")]
    [SerializeField] private Renderer boardRenderer;

    [Header("URP/Lit")]
    [SerializeField] private string textureProperty = "_BaseMap"; // correct for URP/Lit

    [Header("Library Location")]
    [SerializeField] private string libraryRootFolderName = "Slides";
    [SerializeField] private string[] extensions = new[] { ".png", ".jpg", ".jpeg" };

    [Header("Debug")]
    [SerializeField] private bool logMaterialInfo = true;

    [Header("State (read-only)")]
    [SerializeField] private string currentDeckName = "";
    [SerializeField] private int currentIndex = 0;
    [SerializeField] private int pageCount = 0;

    public string CurrentDeckName => currentDeckName;
    public int CurrentPageIndex => currentIndex; // 0-based
    public int PageCount => pageCount;

    public event Action OnPageChanged;
    public event Action OnDeckLoaded;

    private readonly List<string> _paths = new();
    private Texture2D _tex;

    private void Awake()
    {
        if (!boardRenderer)
            Debug.LogWarning("BoardSlideDeck: boardRenderer is NOT assigned. Assign classroom/blackBoard MeshRenderer.");

        string root = GetLibraryRootPath();
        if (!Directory.Exists(root)) Directory.CreateDirectory(root);

        if (logMaterialInfo && boardRenderer)
        {
            var mat = boardRenderer.sharedMaterial;
            Debug.Log(
                $"BoardSlideDeck boardRenderer='{boardRenderer.name}' " +
                $"Material='{(mat ? mat.name : "NULL")}' Shader='{(mat ? mat.shader.name : "NULL")}' " +
                $"persistentDataPath='{Application.persistentDataPath}'"
            );
        }
    }

    public string GetLibraryRootPath()
        => Path.Combine(Application.persistentDataPath, libraryRootFolderName);

    public List<string> GetAvailableDeckNames()
    {
        string root = GetLibraryRootPath();
        if (!Directory.Exists(root)) return new List<string>();

        return Directory.GetDirectories(root)
            .Select(Path.GetFileName)
            .OrderBy(n => n)
            .ToList();
    }

    public bool LoadDeck(string deckName)

    {
        Debug.Log($"BoardSlideDeck: LoadDeck('{deckName}') instanceID={GetInstanceID()} boardRenderer={(boardRenderer ? boardRenderer.name : "NULL")}");

        if (string.IsNullOrEmpty(deckName)) return false;

        string deckPath = Path.Combine(GetLibraryRootPath(), deckName);
        if (!Directory.Exists(deckPath))
        {
            Debug.LogWarning($"BoardSlideDeck: Deck folder not found: {deckPath}");
            return false;
        }

        _paths.Clear();

        var files = Directory.GetFiles(deckPath)
            .Where(p => extensions.Contains(Path.GetExtension(p).ToLowerInvariant()))
            .OrderBy(p => p)
            .ToList();

        _paths.AddRange(files);
        pageCount = _paths.Count;

        if (pageCount == 0)
        {
            Debug.LogWarning($"BoardSlideDeck: Deck '{deckName}' has no images. Put 001.png, 002.png... in: {deckPath}");
            return false;
        }

        currentDeckName = deckName;
        currentIndex = 0;

        ShowPage(0);

        OnDeckLoaded?.Invoke();
        return true;
    }

    public void NextPage()
    {
        if (_paths.Count == 0) return;
        ShowPage(currentIndex + 1);
    }

    public void PrevPage()
    {
        if (_paths.Count == 0) return;
        ShowPage(currentIndex - 1);
    }

    public void ShowPage(int index)
    {
        if (_paths.Count == 0) return;

        currentIndex = Mathf.Clamp(index, 0, _paths.Count - 1);
        string path = _paths[currentIndex];

        if (!File.Exists(path))
        {
            Debug.LogWarning($"BoardSlideDeck: File missing: {path}");
            return;
        }

        byte[] bytes = File.ReadAllBytes(path);

        if (_tex == null)
            _tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);

        _tex.LoadImage(bytes, markNonReadable: true);
        _tex.Apply(false, true);

        ApplyToBoard(_tex);

        if (logMaterialInfo)
            Debug.Log($"BoardSlideDeck showing '{currentDeckName}' page {currentIndex + 1}/{pageCount}: {Path.GetFileName(path)}");

        OnPageChanged?.Invoke();
    }

    private void ApplyToBoard(Texture tex)
    {
        if (!boardRenderer || tex == null) return;

        Material mat = boardRenderer.material;

        if (!mat)
        {
            Debug.LogWarning("BoardSlideDeck: boardRenderer.material is null");
            return;
        }

        if (!mat.HasProperty(textureProperty))
        {
            Debug.LogWarning($"BoardSlideDeck: Material '{mat.name}' does not have property '{textureProperty}'. Shader: {mat.shader.name}");
            return;
        }

        mat.SetTexture(textureProperty, tex);
    }
}
