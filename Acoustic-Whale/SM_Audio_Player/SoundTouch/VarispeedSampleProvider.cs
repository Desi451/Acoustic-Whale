﻿using System;
using NAudio.Wave;

namespace SM_Audio_Player.SoundTouch;

internal class VarispeedSampleProvider : ISampleProvider, IDisposable
{
    private readonly ISampleProvider _sourceProvider;
    private readonly SoundTouch _soundTouch;
    private readonly float[] _sourceReadBuffer;
    private readonly float[] _soundTouchReadBuffer;
    private readonly int _channelCount;
    private float _playbackRate = 1.0f;
    private SoundTouchProfile? _currentSoundTouchProfile;
    private bool _repositionRequested;

    public VarispeedSampleProvider(ISampleProvider sourceProvider, int readDurationMilliseconds,
        SoundTouchProfile? soundTouchProfile)
    {
        _soundTouch = new SoundTouch();
        // explore what the default values are before we change them:
        //Debug.WriteLine(String.Format("SoundTouch Version {0}", soundTouch.VersionString));
        //Debug.WriteLine("Use QuickSeek: {0}", soundTouch.GetUseQuickSeek());
        //Debug.WriteLine("Use AntiAliasing: {0}", soundTouch.GetUseAntiAliasing());

        SetSoundTouchProfile(soundTouchProfile);
        _sourceProvider = sourceProvider;
        _soundTouch.SetSampleRate(WaveFormat.SampleRate);
        _channelCount = WaveFormat.Channels;
        _soundTouch.SetChannels(_channelCount);
        _sourceReadBuffer = new float[WaveFormat.SampleRate * _channelCount * (long)readDurationMilliseconds / 1000];
        _soundTouchReadBuffer = new float[_sourceReadBuffer.Length * 10]; // support down to 0.1 speed
    }

    public int Read(float[] buffer, int offset, int count)
    {
        if (_playbackRate == 0) // play silence
        {
            for (var n = 0; n < count; n++) buffer[offset++] = 0;
            return count;
        }

        if (_repositionRequested)
        {
            _soundTouch.Clear();
            _repositionRequested = false;
        }

        var samplesRead = 0;
        var reachedEndOfSource = false;
        while (samplesRead < count)
        {
            if (_soundTouch.NumberOfSamplesAvailable == 0)
            {
                var readFromSource = _sourceProvider.Read(_sourceReadBuffer, 0, _sourceReadBuffer.Length);
                if (readFromSource > 0)
                {
                    _soundTouch.PutSamples(_sourceReadBuffer, readFromSource / _channelCount);
                }
                else
                {
                    reachedEndOfSource = true;
                    // we've reached the end, tell SoundTouch we're done
                    _soundTouch.Flush();
                }
            }

            var desiredSampleFrames = (count - samplesRead) / _channelCount;

            var received = _soundTouch.ReceiveSamples(_soundTouchReadBuffer, desiredSampleFrames) * _channelCount;
            // use loop instead of Array.Copy due to WaveBuffer
            for (var n = 0; n < received; n++) buffer[offset + samplesRead++] = _soundTouchReadBuffer[n];
            if (received == 0 && reachedEndOfSource) break;
        }

        return samplesRead;
    }

    public WaveFormat WaveFormat => _sourceProvider.WaveFormat;

    public float PlaybackRate
    {
        get => _playbackRate;
        set
        {
            if (_playbackRate != value)
            {
                UpdatePlaybackRate(value);
                _playbackRate = value;
            }
        }
    }

    private void UpdatePlaybackRate(float value)
    {
        if (value != 0)
        {
            if (_currentSoundTouchProfile != null && _currentSoundTouchProfile.UseTempo)
                _soundTouch.SetTempo(value);
            else
                _soundTouch.SetRate(value);
        }
    }

    public void Dispose()
    {
        _soundTouch.Dispose();
    }

    public void SetSoundTouchProfile(SoundTouchProfile? soundTouchProfile)
    {
        if (_currentSoundTouchProfile != null &&
            soundTouchProfile != null &&
            _playbackRate != 1.0f &&
            soundTouchProfile.UseTempo != _currentSoundTouchProfile.UseTempo)
        {
            if (soundTouchProfile.UseTempo)
            {
                _soundTouch.SetRate(1.0f);
                _soundTouch.SetPitchOctaves(0f);
                _soundTouch.SetTempo(_playbackRate);
            }
            else
            {
                _soundTouch.SetTempo(1.0f);
                _soundTouch.SetRate(_playbackRate);
            }
        }

        _currentSoundTouchProfile = soundTouchProfile;
        _soundTouch.SetUseAntiAliasing(soundTouchProfile != null && soundTouchProfile.UseAntiAliasing);
        _soundTouch.SetUseQuickSeek(soundTouchProfile != null && soundTouchProfile.UseQuickSeek);
    }

    public void Reposition()
    {
        _repositionRequested = true;
    }
}