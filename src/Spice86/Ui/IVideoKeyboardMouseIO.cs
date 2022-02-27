﻿namespace Spice86.Ui;

using Avalonia.Input;

using Spice86.Emulator.Devices.Video;
using Spice86.Ui.ViewModels;

using System;
using System.Collections.Generic;

public interface IVideoKeyboardMouseIO {

    bool IsPaused { get; }

    /// <summary>
    /// Makes sure it's safe to continue (ie. a pause wasn't requested)
    /// </summary>
    void WaitOne();

    void AddBuffer(uint address, double scale, int bufferWidth, int bufferHeight, bool isPrimaryDisplay = false);

    void Draw(byte[] memory, Rgb[] palette);

    int Height { get; }

    Key? LastKeyCode { get; }

    int MouseX { get; set; }

    int MouseY { get; set; }

    IDictionary<uint, VideoBufferViewModel> VideoBuffersAsDictionary { get; }

    int Width { get; }

    bool IsKeyPressed(Key keyCode);

    bool IsLeftButtonClicked { get; }

    bool IsRightButtonClicked { get; }

    void RemoveBuffer(uint address);

    void SetOnKeyPressedEvent(Action onKeyPressedEvent);

    void SetOnKeyReleasedEvent(Action onKeyReleasedEvent);

    void SetResolution(int width, int height, uint address);
}