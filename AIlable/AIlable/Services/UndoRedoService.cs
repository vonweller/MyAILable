using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AIlable.Services;

public interface IUndoableCommand
{
    void Execute();
    void Undo();
    string Description { get; }
}

public class UndoRedoService : ObservableObject
{
    private readonly Stack<IUndoableCommand> _undoStack = new();
    private readonly Stack<IUndoableCommand> _redoStack = new();
    private const int MaxUndoLevels = 50;

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;
    
    public string? LastUndoDescription => _undoStack.Count > 0 ? _undoStack.Peek().Description : null;
    public string? LastRedoDescription => _redoStack.Count > 0 ? _redoStack.Peek().Description : null;

    public void ExecuteCommand(IUndoableCommand command)
    {
        try
        {
            command.Execute();
            
            _undoStack.Push(command);
            _redoStack.Clear();
            
            // 限制撤销历史记录的数量
            if (_undoStack.Count > MaxUndoLevels)
            {
                var oldCommands = _undoStack.ToArray();
                _undoStack.Clear();
                for (int i = oldCommands.Length - MaxUndoLevels; i < oldCommands.Length; i++)
                {
                    _undoStack.Push(oldCommands[i]);
                }
            }
            
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
            OnPropertyChanged(nameof(LastUndoDescription));
            OnPropertyChanged(nameof(LastRedoDescription));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Command execution failed: {ex.Message}");
        }
    }

    public void Undo()
    {
        if (!CanUndo) return;

        try
        {
            var command = _undoStack.Pop();
            command.Undo();
            _redoStack.Push(command);
            
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
            OnPropertyChanged(nameof(LastUndoDescription));
            OnPropertyChanged(nameof(LastRedoDescription));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Undo operation failed: {ex.Message}");
        }
    }

    public void Redo()
    {
        if (!CanRedo) return;

        try
        {
            var command = _redoStack.Pop();
            command.Execute();
            _undoStack.Push(command);
            
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
            OnPropertyChanged(nameof(LastUndoDescription));
            OnPropertyChanged(nameof(LastRedoDescription));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Redo operation failed: {ex.Message}");
        }
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(LastUndoDescription));
        OnPropertyChanged(nameof(LastRedoDescription));
    }
}