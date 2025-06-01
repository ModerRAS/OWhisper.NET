using System;
using System.Collections.Generic;
using OWhisper.Core.Models;

namespace OWhisper.Core.Services
{
    public interface ITranscriptionQueueService
    {
        event EventHandler<TranscriptionTask> ProgressUpdated;
        
        string EnqueueTask(byte[] audioData, string fileName, string language = null, string model = null);
        TranscriptionTask GetTask(string taskId);
        List<TranscriptionTask> GetAllTasks();
        bool CancelTask(string taskId);
        int GetQueueLength();
        void Start();
        void Stop();
    }
} 