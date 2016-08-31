﻿using System;
using System.Collections.Generic;
using Syntage.Framework.Parameters;
using Syntage.Framework.Tools;
using Syntage.Logic.Audio;

namespace Syntage.Logic
{
    public class ADSR : AudioProcessorPartWithParameters, IProcessor
    {
        private class NoteEnvelope
        {
            private enum EState
            {
                None,
                Attack,
                Decay,
                Sustain,
                Release
            }

            private readonly ADSR _ownerEnvelope;
			private double _timeDelta;
			private double _time;
			private double _multiplier;
	        private double _startMultiplier;
            private EState _state;
            
            public NoteEnvelope(ADSR owner)
	        {
		        _ownerEnvelope = owner;
	        }

			public double GetNextMultiplier()
			{
				_multiplier = 0;

		        switch (_state)
		        {
					case EState.Attack:
                        _multiplier = DSPFunctions.Lerp(_startMultiplier, 1, 1 - _time / _ownerEnvelope.Attack.Value);
						if (_time < 0)
							SetState(EState.Decay);
						break;

					case EState.Decay:
						_multiplier = DSPFunctions.Lerp(_startMultiplier, _ownerEnvelope.Sustain.Value, 1 - _time / _ownerEnvelope.Decay.Value);
						if (_time < 0)
							SetState(EState.Sustain);
						break;

					case EState.Sustain:
						_multiplier = _ownerEnvelope.Sustain.Value;
						break;

					case EState.Release:
						_multiplier = DSPFunctions.Lerp(_startMultiplier, 0, 1 - _time / _ownerEnvelope.Release.Value);
						if (_time < 0)
							SetState(EState.None);
						break;
		        }

				_time -= _timeDelta;
				return _multiplier;
			}

	        public void Press()
            {
                SetState(EState.Attack);
	        }

	        public void Release()
	        {
		        SetState(EState.Release);
			}

			private void SetState(EState newState)
			{
				switch (newState)
				{
					case EState.None:
						break;
                        
                    case EState.Attack:
                        _timeDelta = 1.0 / _ownerEnvelope.Processor.SampleRate;
                        _time = _ownerEnvelope.Attack.Value;
						break;

					case EState.Decay:
						_time = _ownerEnvelope.Decay.Value;
						break;

					case EState.Sustain:
						break;

					case EState.Release:
						_time = _ownerEnvelope.Release.Value;
						break;

					default:
						throw new ArgumentOutOfRangeException();
				}

				_startMultiplier = _multiplier;
                _state = newState;
			}
		}

        private readonly NoteEnvelope _noteEnvelope;
	    private int _lastPressedNotesCount;

		public RealParameter Attack { get; private set; }
	    public RealParameter Decay { get; private set; }
	    public RealParameter Sustain { get; private set; }
	    public RealParameter Release { get; private set; }

	    public ADSR(AudioProcessor audioProcessor) : base(audioProcessor)
	    {
	        _noteEnvelope = new NoteEnvelope(this);

            Processor.Input.OnPressedNotesChanged += OnPressedNotesChanged;
        }

	    public override IEnumerable<Parameter> CreateParameters(string parameterPrefix)
	    {
		    Attack = new RealParameter(parameterPrefix + "Atk", "Envelope Attack", "", 0.01, 1, 0.01);
		    Decay = new RealParameter(parameterPrefix + "Dec", "Envelope Decay", "", 0.01, 1, 0.01);
		    Sustain = new RealParameter(parameterPrefix + "Stn", "Envelope Sustain", "", 0, 1, 0.01);
		    Release = new RealParameter(parameterPrefix + "Rel", "Envelope Release", "", 0.01, 1, 0.01);

		    return new List<Parameter> {Attack, Decay, Sustain, Release};
	    }

        public void Process(IAudioStream stream)
        {
            var lc = stream.Channels[0];
            var rc = stream.Channels[1];

            var count = Processor.CurrentStreamLenght;
            for (int i = 0; i < count; ++i)
            {
                var multiplier = _noteEnvelope.GetNextMultiplier();
                lc.Samples[i] *= multiplier;
                rc.Samples[i] *= multiplier;
            }
        }

		private void OnPressedNotesChanged(object sender, EventArgs e)
        {
            var currentPressedNotesCount = Processor.Input.PressedNotesCount;
            if (currentPressedNotesCount > 0
				&& _lastPressedNotesCount == 0)
            {
                _noteEnvelope.Press();
            }
            else if (currentPressedNotesCount == 0
				&& _lastPressedNotesCount > 0)
            {
                _noteEnvelope.Release();
            }

			_lastPressedNotesCount = currentPressedNotesCount;
        }
    }
}
