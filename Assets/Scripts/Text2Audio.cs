using Baidu.Aip.Speech;
using NAudio.Wave;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Text2Audio : MonoBehaviour
{
    public Text _networkState;
    public Button _refreshBtn;
    public CanvasGroup _synthesisPanel;

    //appSettings
    public InputField _appId;
    public InputField _apiKey;
    public InputField _scretKey;

    //synthesisSettings
    public Toggle _batchMode;
    public InputField _singleText;
    public Text _mutipleText;
    public Slider _speed;
    public Text _speedValue;
    public Slider _pitch;
    public Text _pitchValue;
    public Slider _volume;
    public Text _volumeValue;
    public Dropdown _person;

    //saveSettings
    public InputField _savePath;
    public InputField _saveName;
    public Dropdown _saveType;
    public Button _saveBtn;
    public AudioSource _audioSource;
    public Button _playBtn;

    //debug
    public ScrollRect _debugPanel;
    public Text _debug;

    private Tts _client;
    private Dictionary<string, int> _speakerDict;
    private byte[] _synthesisResult;
    private Dictionary<string, string> _textDict;

    private void Start()
    {
        InitForm();
        InitClient();
    }

    private void InitForm()
    {
        RefreshNetwork();
        _refreshBtn.onClick.AddListener(RefreshNetwork);

        _batchMode.onValueChanged.AddListener(InitBatchMode);

        _singleText.text = "阳光彩虹小白马";
        _singleText.onEndEdit.AddListener(s => ShowDebug(null, true));
        _speed.onValueChanged.AddListener(f => _speedValue.text = _speed.value.ToString());
        _speed.wholeNumbers = true;
        _speed.minValue = 0f;
        _speed.maxValue = 9f;
        _speed.value = 5f;

        _pitch.onValueChanged.AddListener(f => _pitchValue.text = _pitch.value.ToString());
        _pitch.wholeNumbers = true;
        _pitch.minValue = 0f;
        _pitch.maxValue = 9f;
        _pitch.value = 5f;

        _volume.onValueChanged.AddListener(f => _volumeValue.text = _volume.value.ToString());
        _volume.wholeNumbers = true;
        _volume.minValue = 0f;
        _volume.maxValue = 15f;
        _volume.value = 5f;

        _speakerDict = new Dictionary<string, int>
        {
            {"普通女声", 0},
            {"普通男声", 1},
            {"特别男声", 2},
            {"情感男声（度逍遥）", 3},
            {"情感儿声（度丫丫）", 4},
            {"情感女声（度小娇）", 5},
            {"情感儿声（度米朵）", 103},
            {"情感男声（度博文）", 106},
            {"情感儿声（度小童）", 110},
            {"情感女声（度小萌）", 111},
        };
        var speakerOptions = new List<Dropdown.OptionData>();
        foreach (var item in _speakerDict)
        {
            speakerOptions.Add(new Dropdown.OptionData(item.Key));
        }

        _person.options = speakerOptions;

        _playBtn.onClick.AddListener(() => StartCoroutine(PlayAudioClip()));

        _savePath.text = Application.streamingAssetsPath;
        _saveName.text = "audioclip1";
        _saveType.options = new List<Dropdown.OptionData>() {new Dropdown.OptionData("wav"), new Dropdown.OptionData("mp3")};
        _saveBtn.onClick.AddListener(SaveAudioClip);

        ShowDebug(null, true);
    }

    private void RefreshNetwork()
    {
        switch (Application.internetReachability)
        {
            case NetworkReachability.ReachableViaCarrierDataNetwork:
                _networkState.text = "当前网络状态：<color=green>已连接蜂窝网络</color>（点击刷新）";
                _synthesisPanel.interactable = true;
                break;
            case NetworkReachability.ReachableViaLocalAreaNetwork:
                _networkState.text = "当前网络状态：<color=green>已连接局域网</color>（点击刷新）";
                _synthesisPanel.interactable = true;
                break;
            default:
                _networkState.text = "当前网络状态：<color=red>未连接</color>（点击刷新）";
                _synthesisPanel.interactable = false;
                break;
        }
    }

    private void InitClient()
    {
        var appId = _appId.text;
        var apiKey = _apiKey.text;
        var secretKey = _scretKey.text;
        _client = new Tts(apiKey, secretKey)
        {
            Timeout = 60000 //超时时间
        };
    }

    private void LoadTextAsset()
    {
        var doc = new XmlDocument();
        try
        {
            doc.Load(Application.streamingAssetsPath + "/TextAsset.xml");
        }
        catch
        {
            ShowDebug("【错误】文件读取错误！文件地址：" + Application.streamingAssetsPath + "/TextAsset.xml");
            return;
        }

        _textDict = new Dictionary<string, string>();
        if (doc.DocumentElement != null)
        {
            foreach (XmlNode item in doc.DocumentElement.ChildNodes)
            {
                if (item.Attributes != null) _textDict.Add(item.Attributes["name"].Value, item.Attributes["text"].Value);
            }
        }
    }

    private void InitBatchMode(bool batch)
    {
        _singleText.gameObject.SetActive(!batch);
        _saveName.interactable = !batch;
        _playBtn.interactable = !batch;

        ShowDebug(null, true);
        LoadTextAsset();

        StringBuilder sb = new StringBuilder("【显示格式】：\n文件名————文本（只显示前20个字符）\n【批量合成文本】：\n");
        foreach (var item in _textDict)
        {
            sb.Append(item.Key);
            sb.Append("————");

            var text = item.Value;
            if (text.Length > 20)
            {
                text = text.Substring(0, 20);
            }

            sb.Append(text);
            sb.AppendLine();
        }

        _mutipleText.text = sb.ToString();
    }

    private void ShowDebug(string msg, bool clear = false)
    {
        if (clear)
        {
            _debug.text = "【日志】:";
            return;
        }

        _debug.text += "\n" + msg;
        LayoutRebuilder.ForceRebuildLayoutImmediate(_debug.rectTransform);
        _debugPanel.verticalNormalizedPosition = 0f;
    }

    private void SaveAudioClip()
    {
        if (_batchMode)
        {
            SaveMutipleAudioClips();
        }
        else
        {
            SaveSingleAudioClip();
        }
    }

    private void SaveSingleAudioClip()
    {
        var savePath = _savePath.text;
        if (savePath == "")
        {
            ShowDebug("【错误】保存地址为空！");
            return;
        }

        if (!Directory.Exists(savePath))
        {
            Directory.CreateDirectory(savePath);
        }

        var saveName = _saveName.text;
        if (saveName == "")
        {
            ShowDebug("【错误】保存文件名为空！");
            return;
        }

        var current = EventSystem.current;
        current.enabled = false;
        StartCoroutine(SaveAudioClip(_singleText.text, savePath, saveName));
        current.enabled = true;
    }

    private void SaveMutipleAudioClips()
    {
        var savePath = _savePath.text;
        if (savePath == "")
        {
            ShowDebug("【错误】保存地址为空！");
            return;
        }

        if (!Directory.Exists(savePath))
        {
            Directory.CreateDirectory(savePath);
        }

        StartCoroutine(SaveAudioClip(savePath));
    }

    private IEnumerator SaveAudioClip(string savePath)
    {
        var current = EventSystem.current;
        current.enabled = false;
        var wait = new WaitForSeconds(1);
        foreach (var item in _textDict)
        {
            yield return SaveAudioClip(item.Value, savePath, item.Key);
            yield return wait; //普通用户有并发限制
        }

        current.enabled = true;
        ShowDebug("【完成】批量合成已完成！");
    }

    private IEnumerator SaveAudioClip(string text, string savePath, string saveName)
    {
        yield return Synthesis(text);
        if (_synthesisResult == null)
        {
            yield break;
        }

        if (_saveType.value == 0)
        {
            var path = $"{savePath}/{saveName}.wav";
            using (var memory = new MemoryStream(_synthesisResult))
            {
                using (var reader = new Mp3FileReader(memory))
                {
                    WaveFileWriter.CreateWaveFile(path, reader);
                }
            }

            ShowDebug("【保存】" + path);
        }
        else
        {
            var path = $"{savePath}/{saveName}.mp3";
            File.WriteAllBytes(path, _synthesisResult);
            ShowDebug("【保存】" + path);
        }
    }

    private IEnumerator PlayAudioClip()
    {
        yield return Synthesis(_singleText.text);
        if (_synthesisResult == null)
        {
            yield break;
        }

        using (var memory = new MemoryStream(_synthesisResult))
        {
            using (var reader = new Mp3FileReader(memory))
            {
                using (var outStream = new MemoryStream())
                {
                    WaveFileWriter.WriteWavFileToStream(outStream, reader);
                    _audioSource.clip = WavUtility.ToAudioClip(outStream.ToArray(), 0);
                }
            }
        }

        _audioSource.Play();
    }

    private IEnumerator Synthesis(string text)
    {
        if (text == null)
        {
            ShowDebug("【错误】语音合成文本为空！");
            yield break;
        }

        if (_client == null)
        {
            InitClient();
        }

        var option = new Dictionary<string, object>
        {
            {"spd", _speed.value},
            {"pit", _pitch.value},
            {"vol", _volume.value},
            {"per", _speakerDict[_person.captionText.text]}
        };
        var result = _client.Synthesis(text, option);

        if (result.Success)
        {
            _synthesisResult = result.Data;
            ShowDebug("【成功】语音合成成功！");
        }
        else
        {
            _synthesisResult = null;
            ShowDebug($"【错误】语音合成失败！错误代码：{result.ErrorCode}，错误信息：{result.ErrorMsg}。");
            yield break;
        }
    }
}