﻿using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Labs.FacialRemote
{
    /// <inheritdoc cref="IStreamSource" />
    /// <summary>
    /// Reads tracking data from a PlaybackData asset and updates a Stream Reader.
    /// </summary>
    public class PlaybackStream : MonoBehaviour, IStreamSource, IStreamRecorder
    {
        [SerializeField]
        [Tooltip("Contains the individual streams recorded from a capture session.")]
        PlaybackData m_PlaybackData;
        
        float m_PlaybackStartTime = float.PositiveInfinity;

        int m_BufferPosition;

        byte[] m_CurrentFrameBuffer;
        readonly float[] m_FrameTime = new float[1];
        float m_FirstFrameTime;
        float m_NextFrameTime;

        [NonSerialized]
        PlaybackBuffer m_ActivePlaybackBuffer;
        
        List<IStreamReader> m_StreamReaders = new List<IStreamReader>();

        public List<IStreamReader> streamReaders
        {
            get { return m_StreamReaders; }
        }

        public bool isActive { get; private set; }
        public PlaybackBuffer activePlaybackBuffer { get { return m_ActivePlaybackBuffer; } }
        public IStreamSettings streamSettings { get { return activePlaybackBuffer; } }

        public PlaybackData playbackData
        {
            get { return m_PlaybackData; }
            set { m_PlaybackData = value; }
        }

        void Start()
        {
            if (!m_PlaybackData)
                Debug.LogWarningFormat("No Playback Data set on {0}. You will be unable to record, playback or bake any stream data.",
                    gameObject.name);
        }

        public void StreamSourceUpdate()
        {
            isActive = false;
            foreach (var sr in streamReaders)
            {
                var source = sr.streamSource;
                if (source != null && source.Equals(this))
                {
                    isActive = true;
                    break;
                }
            }

            if (!isActive)
                return;

            if (Time.time - m_PlaybackStartTime < m_NextFrameTime - m_FirstFrameTime)
                return;

            if (!PlayBackLoop())
                StopPlayback();

            UpdateCurrentFrameBuffer();
        }

        public void SetPlaybackBuffer(PlaybackBuffer buffer)
        {
            if (isActive)
                StopPlayback();

            m_ActivePlaybackBuffer = buffer;
        }

        public void StartPlayback()
        {
            if (activePlaybackBuffer == null)
            {
                Debug.Log("No Playback Buffer Set.");
                SetPlaybackBuffer(playbackData.playbackBuffers[0]);
            }

            var settings = activePlaybackBuffer;
            m_CurrentFrameBuffer = new byte[settings.bufferSize];
            for (var i = 0; i < settings.bufferSize; i++)
            {
                m_CurrentFrameBuffer[i] = 0;
            }

            Buffer.BlockCopy(activePlaybackBuffer.recordStream, 0, m_CurrentFrameBuffer, 0, streamSettings.bufferSize);
            Buffer.BlockCopy(m_CurrentFrameBuffer, streamSettings.FrameTimeOffset, m_FrameTime, 0, streamSettings.FrameTimeSize);

            m_PlaybackStartTime = Time.time;
            m_FirstFrameTime = m_FrameTime[0];
            m_NextFrameTime = m_FirstFrameTime;
            m_BufferPosition = 0;

            isActive = true;
        }

        public void StopPlayback()
        {
            m_PlaybackStartTime = float.PositiveInfinity;
            isActive = false;
        }

        public bool PlayBackLoop()
        {
            if (m_BufferPosition + streamSettings.bufferSize > activePlaybackBuffer.recordStream.Length)
                return false;

            Buffer.BlockCopy(activePlaybackBuffer.recordStream, m_BufferPosition,
                m_CurrentFrameBuffer, 0, streamSettings.bufferSize);
            Buffer.BlockCopy(m_CurrentFrameBuffer, streamSettings.FrameTimeOffset, m_FrameTime,
                0, streamSettings.FrameTimeSize);

            m_BufferPosition += streamSettings.bufferSize;
            m_NextFrameTime = m_FrameTime[0];

            return true;
        }

        public void UpdateCurrentFrameBuffer(bool force = false)
        {
            foreach (var sr in streamReaders)
            {
                if (force || sr.streamSource.Equals(this) && isActive)
                    sr.UpdateStreamData(m_CurrentFrameBuffer);
            }
        }

        public void StartRecording(IStreamSettings settings, int take)
        {
            playbackData.StartRecording(settings, take);
        }

        public void AddDataToRecording(byte[] buffer, int offset = 0)
        {
            playbackData.AddDataToRecording(buffer, offset);
        }

        public void FinishRecording()
        {
            playbackData.FinishRecording();
        }
    }
}
