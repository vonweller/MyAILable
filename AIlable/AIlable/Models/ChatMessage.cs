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
    File
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

    public bool IsUser => Role == MessageRole.User;
    public bool IsAssistant => Role == MessageRole.Assistant;
    public bool HasImage => Type == MessageType.Image && !string.IsNullOrEmpty(ImageFilePath);
    public bool HasFile => Type == MessageType.File && !string.IsNullOrEmpty(FileName);
    
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
}