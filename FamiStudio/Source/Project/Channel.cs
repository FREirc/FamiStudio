﻿using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace FamiStudio
{
    public class Channel
    {
        // Channel types.
        public const int Square1 = 0;
        public const int Square2 = 1;
        public const int Triangle = 2;
        public const int Noise = 3;
        public const int DPCM = 4;
        public const int ExpansionAudioStart = 5;
        public const int VRC6Square1 = 5;
        public const int VRC6Square2 = 6;
        public const int VRC6Saw = 7;
        public const int Count = 8;

        private Song song;
        private Pattern[] patternInstances = new Pattern[Song.MaxLength];
        private List<Pattern> patterns = new List<Pattern>();
        private int type;

        public int Type => type;
        public string Name => channelNames[(int)type];
        public Song Song => song;
        public Pattern[] PatternInstances => patternInstances;
        public List<Pattern> Patterns => patterns;

        static string[] channelNames =
        {
            "Square 1",
            "Square 2",
            "Triangle",
            "Noise",
            "DPCM",
            "Square 1", // VRC6
            "Square 2", // VRC6
            "Saw", // VRC6
            ""
        };

        public Channel()
        {
            // For serialization
        }

        public Channel(Song song, int type, int songLength)
        {
            this.song = song;
            this.type = type;
        }

        public Pattern GetPattern(string name)
        {
            return patterns.Find(p => p.Name == name);
        }

        public Pattern GetPattern(int id)
        {
            return patterns.Find(p => p.Id == id);
        }

        public bool SupportsInstrument(Instrument instrument)
        {
            if (instrument == null)
                return type == DPCM;

            return true;

            // TODO
            //if (instrument.Type == Instrument.Apu && type < ExpansionAudioStart)
            //    return true;

            //if (instrument.Type == Instrument.Vrc6 && type >= VRC6Square1 && type <= VRC6Saw)
            //    return true;

            //return false;
        }

        public bool SupportsReleaseNotes()
        {
            return type != DPCM;
        }

        public void Split(int factor)
        {
            if ((song.PatternLength % factor) == 0)
            {
                // TODO: This might generate identical patterns, need to cleanup.
                var splitPatterns = new Dictionary<Pattern, Pattern[]>();
                var newPatterns = new List<Pattern>();

                foreach (var pattern in patterns)
                {
                    var splits = pattern.Split(factor);
                    splitPatterns[pattern] = splits;
                    newPatterns.AddRange(splits);
                }

                var newSongLength = song.Length * factor;
                var newPatternsInstances = new Pattern[Song.MaxLength];
                
                for (int i = 0; i < song.Length; i++)
                {
                    var oldPattern = patternInstances[i];
                    if (oldPattern != null)
                    {
                        for (int j = 0; j < factor; j++)
                        {
                            newPatternsInstances[i * factor + j] = splitPatterns[oldPattern][j];
                        }
                    }
                }

                patternInstances = newPatternsInstances;
                patterns = newPatterns;
            }
        }

        public Pattern CreatePattern(string name = null)
        {
            if (name == null)
            {
                name = GenerateUniquePatternName();
            }
            else if (!IsPatternNameUnique(name))
            {
                Debug.Assert(false);
                return null;
            }

            var pat = new Pattern(song.Project.GenerateUniqueId(), song, type, name);
            patterns.Add(pat);
            return pat;
        }

        public void ColorizePatterns()
        {
            foreach (var pat in patterns)
            {
                pat.Color = ThemeBase.RandomCustomColor();
            }
        }

        public void RemoveEmptyPatterns()
        {
            for (int i = 0; i < patternInstances.Length; i++)
            {
                if (patternInstances[i] != null && !patternInstances[i].HasAnyNotes)
                {
                    patternInstances[i] = null;
                }
            }

            CleanupUnusedPatterns();
        }

        public bool RenamePattern(Pattern pattern, string name)
        {
            if (pattern.Name == name)
                return true;

            if (patterns.Find(p => p.Name == name) == null)
            {
                pattern.Name = name;
                return true;
            }

            return false;
        }

        public bool IsPatternNameUnique(string name)
        {
            return patterns.Find(p => p.Name == name) == null;
        }

        public string GenerateUniquePatternName(string baseName = null)
        {
            for (int i = 1; ; i++)
            {
                string name = (baseName != null ? baseName : "Pattern ") + i;
                if (IsPatternNameUnique(name))
                {
                    return name;
                }
            }
        }

        public bool GetMinMaxNote(out Note min, out Note max)
        {
            bool valid = false;

            min = new Note() { Value = 255 };
            max = new Note() { Value = 0 };

            for (int i = 0; i < song.Length; i++)
            {
                var pattern = PatternInstances[i];
                if (pattern != null)
                {
                    for (int j = 0; j < song.PatternLength; j++)
                    {
                        var n = pattern.Notes[j];
                        if (n.IsValid && !n.IsStop)
                        {
                            if (n.Value < min.Value) min = n;
                            if (n.Value > max.Value) max = n;
                            valid = true;
                        }
                    }
                }
            }

            return valid;
        }

        public void CleanupUnusedPatterns()
        {
            HashSet<Pattern> usedPatterns = new HashSet<Pattern>();
            for (int i = 0; i < song.Length; i++)
            {
                var inst = patternInstances[i];
                if (inst != null)
                {
                    usedPatterns.Add(inst);
                }
            }

            patterns.Clear();
            patterns.AddRange(usedPatterns);
        }

        public byte GetLastValidVolume(int startPatternIdx)
        {
            var lastVolume = (byte)Note.VolumeInvalid;

            for (int p = startPatternIdx; p >= 0 && lastVolume == Note.VolumeInvalid; p--)
            {
                var pattern = patternInstances[p];
                if (pattern != null)
                {
                    lastVolume = pattern.LastVolumeValue;
                    if (lastVolume != Note.VolumeInvalid)
                    {
                        return lastVolume;
                    }
                }
            }

            return Note.VolumeMax;
        }

        public int GetLastValidNote(int patternIdx, out Note lastNote, out bool released)
        {
            var lastTime = int.MinValue;
            var invalidNote = new Note() { Value = Note.NoteInvalid }; ;

            lastNote = invalidNote;
            released = false;

            // Find previous valid note.
            for (int p = patternIdx; p >= 0 && lastTime == int.MinValue; p--)
            {
                var pattern = patternInstances[p];
                if (pattern != null)
                {
                    var note = invalidNote;
                    if (pattern.LastValidNoteTime >= 0)
                        note = pattern.LastValidNote;

                    released = note.IsStop ? false : released || pattern.LastValidNoteReleased;

                    if (note.IsValid)
                    {
                        lastNote = note;
                        return pattern.LastValidNoteTime;
                    }
                }
            }

            return int.MinValue;
        }

        private int FindSlideNoteDuration(int patternIdx, int noteIdx)
        {
            var nextPatternIdx = patternIdx;
            var nextNoteIdx    = noteIdx;

            var pattern = patternInstances[nextPatternIdx];
            Debug.Assert(pattern.Notes[nextNoteIdx].IsSlideNote);

            nextNoteIdx++;
            while (nextNoteIdx < song.PatternLength && !pattern.Notes[nextNoteIdx].IsValid) nextNoteIdx++;

            if (nextNoteIdx < song.PatternLength)
                return (nextPatternIdx * song.PatternLength + nextNoteIdx) - (patternIdx * song.PatternLength + noteIdx);

            nextPatternIdx++;
            while (nextPatternIdx < song.Length)
            {
                pattern = patternInstances[nextPatternIdx];
                if (pattern != null && pattern.FirstValidNoteTime >= 0)
                {
                    nextNoteIdx = pattern.FirstValidNoteTime;
                    return (nextPatternIdx * song.PatternLength + nextNoteIdx) - (patternIdx * song.PatternLength + noteIdx);
                }

                nextPatternIdx++;
            }

            return -1;
        }

        public bool ComputeSlideNoteParams(int patternIdx, int noteIdx, int prevNote, out int pitchDelta, out int stepCount, out int stepSize)
        {
            pitchDelta = 0;
            stepCount  = 0;
            stepSize   = 0;

            int noteDuration = FindSlideNoteDuration(patternIdx, noteIdx);

            if (noteDuration < 0)
                noteDuration = 255;

            var currNote  = patternInstances[patternIdx].Notes[noteIdx].Value;
            var noteTable = NesApu.GetNoteTableForChannelType(type, false);

            pitchDelta = (int)noteTable[prevNote] - (int)noteTable[currNote];

            if (pitchDelta != 0)
            {
                var frameCount = Math.Min(noteDuration * song.Speed, 255);
                var floatStep = Math.Abs(pitchDelta) / (float)frameCount;

                stepSize  = (int)Math.Ceiling(floatStep) * -Math.Sign(pitchDelta);
                stepCount = Math.Abs(pitchDelta / stepSize);

                Debug.Assert(stepCount < 255);
                Debug.Assert(Math.Abs(stepSize) < 128);
                Debug.Assert(Math.Abs(stepSize * stepCount) <= Math.Abs(pitchDelta));

                return true;
            }

            return false;
        }

        public bool FindPreviousMatchingNote(int noteValue, ref int patternIdx, ref int noteIdx)
        {
            var pattern = patternInstances[patternIdx];
            if (pattern != null)
            {
                while (noteIdx >= 0 && !pattern.Notes[noteIdx].IsValid) noteIdx--;

                if (noteIdx >= 0)
                    return pattern.Notes[noteIdx].Value == noteValue;
            }

            patternIdx--;
            while (patternIdx >= 0)
            {
                pattern = patternInstances[patternIdx];
                if (pattern != null && pattern.LastValidNoteTime >= 0)
                {
                    if (pattern.LastValidNote.IsValid && 
                        pattern.LastValidNote.Value == noteValue)
                    {
                        noteIdx = pattern.LastValidNoteTime;
                        return true;
                    }
                }

                noteIdx = song.PatternLength - 1;
                patternIdx--;
            }

            return false;
        }

#if DEBUG
        public void Validate(Song song)
        {
            Debug.Assert(this == song.Channels[type]);
            Debug.Assert(this.song == song);
            foreach (var inst in patternInstances)
                Debug.Assert(inst == null || patterns.Contains(inst));
            foreach (var pat in patterns)
                pat.Validate(this);
        }
#endif

        public void SerializeState(ProjectBuffer buffer)
        {
            if (buffer.IsWriting)
                CleanupUnusedPatterns();

            int patternCount = patterns.Count;

            buffer.Serialize(ref song);
            buffer.Serialize(ref patternCount);
            buffer.InitializeList(ref patterns, patternCount);

            foreach (var pattern in patterns)
                pattern.SerializeState(buffer);

            for (int i = 0; i < patternInstances.Length; i++)
                buffer.Serialize(ref patternInstances[i], this);
        }
    }
}
