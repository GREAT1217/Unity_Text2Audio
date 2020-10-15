using NAudio.Wave;
using Baidu.Aip.Speech;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using UnityEngine;
using UnityEngine.UI;
using System;

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
    public InputField _text;
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
    public Text _debug;

    private Tts _client;
    private Dictionary<string, int> _speakerDict;
    private byte[] _synthesisResult;

    void Start()
    {
        InitUI();
        InitClient();
    }

    private void InitUI()
    {
        RefreshNetwork();
        _refreshBtn.onClick.AddListener(RefreshNetwork);

        _appId.text = "22823639";
        _apiKey.text = "fUvRVXe9yBsOlSFUrEA71pjV";
        _scretKey.text = "GXTx67s5kW1pzZ0wyRZQXOoGoq9nU0ck";

        _text.text = "阳光彩虹小白马";
        _text.onEndEdit.AddListener(str => ShowDebug(null, true));
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

        _speakerDict = new Dictionary<string, int>()
        {
            { "普通女声",0},
            { "普通男声",1},
            { "特别男声",2},
            { "情感男声（度逍遥）",3},
            { "情感儿声（度丫丫）",4},
            { "情感女声（度小娇）",5},
            { "情感儿声（度米朵）",103},
            { "情感男声（度博文）",106},
            { "情感儿声（度小童）",110},
            { "情感女声（度小萌）",111},
        };
        var speakerOptions = new List<Dropdown.OptionData>();
        foreach (var item in _speakerDict)
        {
            speakerOptions.Add(new Dropdown.OptionData(item.Key));
        }
        _person.options = speakerOptions;

        _playBtn.onClick.AddListener(PlayAudioClip);

        _savePath.text = Application.streamingAssetsPath;
        _saveName.text = "audioclip1";
        _saveType.options = new List<Dropdown.OptionData>() { new Dropdown.OptionData("wav"), new Dropdown.OptionData("mp3") };
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
        var APP_ID = _appId.text;
        var API_KEY = _apiKey.text;
        var SECRET_KEY = _scretKey.text;
        _client = new Tts(API_KEY, SECRET_KEY)
        {
            Timeout = 60000 //超时时间
        };
    }

    private void Synthesis()
    {
        var text = _text.text;
        if (text == null)
        {
            ShowDebug("【错误】语音合成文本为空！");
            return;
        }

        if (_client == null)
        {
            InitClient();
        }

        var option = new Dictionary<string, object>()
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
        }
        else
        {
            ShowDebug(string.Format("【错误】语音合成失败，错误代码：{0}，错误信息：{1}。", result.ErrorCode, result.ErrorMsg));
        }
    }

    private void ShowDebug(string msg, bool clear = false)
    {
        if (clear)
        {
            _debug.text = "日志:";
            return;
        }

        if (_debug.text != "")
        {
            _debug.text += "\n";
        }

        _debug.text += msg;

#if UNITY_EDITOR
        Debug.Log(msg);
#endif
    }

    private void PlayAudioClip()
    {
        Synthesis();

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

    private void SaveAudioClip()
    {
        if (_synthesisResult == null)
        {
            //为了节省次数
            Synthesis();
        }

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

        if (_saveType.value == 0)
        {
            var path = string.Format("{0}/{1}.wav", savePath, saveName);
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
            var path = string.Format("{0}/{1}.mp3", savePath, saveName);
            File.WriteAllBytes(path, _synthesisResult);
            ShowDebug("【保存】" + path);
        }
    }

}
