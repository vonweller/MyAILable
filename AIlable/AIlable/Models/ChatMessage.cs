using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AIlable.Models;

public enum MessageRole
{
    User,
    Assistant,
    System
}

public enum MessageType
{
    Text,
    Image,
    File,
    Audio,        // 音频消息
    Video,        // 视频消息
    Multimodal    // 多模态消息（包含多种类型）
}

public partial class ChatMessage : ObservableObject
{
    [ObservableProperty] private string _id = Guid.NewGuid().ToString();
    [ObservableProperty] private MessageRole _role;
    [ObservableProperty] private MessageType _type;
    [ObservableProperty] private string _content = string.Empty;
    [ObservableProperty] private string? _imageFilePath;
    [ObservableProperty] private string? _fileName;
    [ObservableProperty] private byte[]? _fileData;
    [ObservableProperty] private DateTime _timestamp = DateTime.Now;
    [ObservableProperty] private bool _isStreaming;
    [ObservableProperty] private Dictionary<string, object> _metadata = new();
    
    // 新增多模态属性
    [ObservableProperty] private string? _audioFilePath;           // 音频文件路径
    [ObservableProperty] private byte[]? _audioData;               // 音频数据
    [ObservableProperty] private string? _audioFormat = "wav";     // 音频格式
    [ObservableProperty] private List<string>? _videoFramePaths;   // 视频帧路径列表
    [ObservableProperty] private List<byte[]>? _videoFrameData;    // 视频帧数据列表
    [ObservableProperty] private bool _hasAudioOutput;             // 是否包含AI音频输出
    [ObservableProperty] private byte[]? _aiAudioData;             // AI生成的音频数据
    [ObservableProperty] private string _aiVoice = "Cherry";       // AI语音类型

    public bool IsUser => Role == MessageRole.User;
    public bool IsAssistant => Role == MessageRole.Assistant;
    public bool HasImage => Type == MessageType.Image && !string.IsNullOrEmpty(ImageFilePath);
    public bool HasFile => Type == MessageType.File && !string.IsNullOrEmpty(FileName);
    public bool HasAudio => Type == MessageType.Audio && (!string.IsNullOrEmpty(AudioFilePath) || AudioData != null);
    public bool HasVideo => Type == MessageType.Video && (VideoFramePaths?.Count > 0 || VideoFrameData?.Count > 0);
    public bool HasMultimodal => Type == MessageType.Multimodal;
    
    public ChatMessage() { }
    
    public ChatMessage(MessageRole role, string content, MessageType type = MessageType.Text)
    {
        Role = role;
        Content = content;
        Type = type;
    }
    
    public static ChatMessage CreateUserTextMessage(string content)
    {
        return new ChatMessage(MessageRole.User, content, MessageType.Text);
    }
    
    public static ChatMessage CreateUserImageMessage(string content, string imagePath)
    {
        return new ChatMessage(MessageRole.User, content, MessageType.Image)
        {
            ImageFilePath = imagePath
        };
    }
    
    public static ChatMessage CreateUserFileMessage(string content, string fileName, byte[] fileData)
    {
        return new ChatMessage(MessageRole.User, content, MessageType.File)
        {
            FileName = fileName,
            FileData = fileData
        };
    }
    
    public static ChatMessage CreateAssistantMessage(string content)
    {
        return new ChatMessage(MessageRole.Assistant, content, MessageType.Text);
    }
    
    // 新增多模态消息创建方法
    public static ChatMessage CreateUserAudioMessage(string content, string audioPath)
    {
        return new ChatMessage(MessageRole.User, content, MessageType.Audio)
        {
            AudioFilePath = audioPath
        };
    }
    
    public static ChatMessage CreateUserAudioMessage(string content, byte[] audioData, string format = "wav")
    {
        return new ChatMessage(MessageRole.User, content, MessageType.Audio)
        {
            AudioData = audioData,
            AudioFormat = format
        };
    }
    
    public static ChatMessage CreateUserVideoMessage(string content, List<string> framePaths)
    {
        return new ChatMessage(MessageRole.User, content, MessageType.Video)
        {
            VideoFramePaths = framePaths
        };
    }
    
    public static ChatMessage CreateUserVideoMessage(string content, List<byte[]> frameData)
    {
        return new ChatMessage(MessageRole.User, content, MessageType.Video)
        {
            VideoFrameData = frameData
        };
    }
    
    public static ChatMessage CreateAssistantMessageWithAudio(string content, byte[] audioData, string voice = "Cherry")
    {
        return new ChatMessage(MessageRole.Assistant, content, MessageType.Text)
        {
            HasAudioOutput = true,
            AiAudioData = audioData,
            AiVoice = voice
        };
    }
}