﻿using System;
using System.Runtime.CompilerServices;
using Disruptor.Dsl;

namespace Disruptor
{
    /// <summary>
    /// Ring based store of reusable entries containing the data representing
    /// an event being exchanged between event producer and <see cref="IEventProcessor"/>s.
    /// </summary>
    /// <typeparam name="T">implementation storing the data for sharing during exchange or parallel coordination of an event.</typeparam>
    public sealed class ValueRingBuffer<T> : RingBuffer, ISequenced, IValueDataProvider<T>
        where T : struct
    {
        /// <summary>
        /// Construct a ValueRingBuffer with the full option set.
        /// </summary>
        /// <param name="sequencer">sequencer to handle the ordering of events moving through the ring buffer.</param>
        /// <exception cref="ArgumentException">if bufferSize is less than 1 or not a power of 2</exception>
        public ValueRingBuffer(ISequencer sequencer)
            : this(() => default(T), sequencer)
        {
        }

        /// <summary>
        /// Construct a ValueRingBuffer with the full option set.
        /// </summary>
        /// <param name="eventFactory">eventFactory to create entries for filling the ring buffer</param>
        /// <param name="sequencer">sequencer to handle the ordering of events moving through the ring buffer.</param>
        /// <exception cref="ArgumentException">if bufferSize is less than 1 or not a power of 2</exception>
        public ValueRingBuffer(Func<T> eventFactory, ISequencer sequencer)
        : base(sequencer, typeof(T))
        {
            Fill(eventFactory);
        }

        private void Fill(Func<T> eventFactory)
        {
            var entries = (T[]) _entries;
            for (int i = 0; i < _bufferSize; i++)
            {
                entries[_bufferPad + i] = eventFactory();
            }
        }

        /// <summary>
        /// Construct a ValueRingBuffer with a <see cref="MultiProducerSequencer"/> sequencer.
        /// </summary>
        /// <param name="eventFactory"> eventFactory to create entries for filling the ring buffer</param>
        /// <param name="bufferSize">number of elements to create within the ring buffer.</param>
        public ValueRingBuffer(Func<T> eventFactory, int bufferSize)
            : this(eventFactory, new MultiProducerSequencer(bufferSize, new BlockingWaitStrategy()))
        {
        }

        /// <summary>
        /// Create a new multiple producer ValueRingBuffer with the specified wait strategy.
        /// </summary>
        /// <param name="factory">used to create the events within the ring buffer.</param>
        /// <param name="bufferSize">number of elements to create within the ring buffer.</param>
        /// <param name="waitStrategy">used to determine how to wait for new elements to become available.</param>
        /// <returns>a constructed ring buffer.</returns>
        /// <exception cref="ArgumentException">if bufferSize is less than 1 or not a power of 2</exception>
        public static ValueRingBuffer<T> CreateMultiProducer(Func<T> factory, int bufferSize, IWaitStrategy waitStrategy)
        {
            MultiProducerSequencer sequencer = new MultiProducerSequencer(bufferSize, waitStrategy);

            return new ValueRingBuffer<T>(factory, sequencer);
        }

        /// <summary>
        /// Create a new multiple producer ValueRingBuffer using the default wait strategy <see cref="BlockingWaitStrategy"/>.
        /// </summary>
        /// <param name="factory">used to create the events within the ring buffer.</param>
        /// <param name="bufferSize">number of elements to create within the ring buffer.</param>
        /// <returns>a constructed ring buffer.</returns>
        /// <exception cref="ArgumentException">if bufferSize is less than 1 or not a power of 2</exception>
        public static ValueRingBuffer<T> CreateMultiProducer(Func<T> factory, int bufferSize)
        {
            return CreateMultiProducer(factory, bufferSize, new BlockingWaitStrategy());
        }

        /// <summary>
        /// Create a new single producer ValueRingBuffer with the specified wait strategy.
        /// </summary>
        /// <param name="factory">used to create the events within the ring buffer.</param>
        /// <param name="bufferSize">number of elements to create within the ring buffer.</param>
        /// <param name="waitStrategy">used to determine how to wait for new elements to become available.</param>
        /// <returns>a constructed ring buffer.</returns>
        /// <exception cref="ArgumentException">if bufferSize is less than 1 or not a power of 2</exception>
        public static ValueRingBuffer<T> CreateSingleProducer(Func<T> factory, int bufferSize, IWaitStrategy waitStrategy)
        {
            SingleProducerSequencer sequencer = new SingleProducerSequencer(bufferSize, waitStrategy);

            return new ValueRingBuffer<T>(factory, sequencer);
        }

        /// <summary>
        /// Create a new single producer ValueRingBuffer using the default wait strategy <see cref="BlockingWaitStrategy"/>.
        /// </summary>
        /// <param name="factory">used to create the events within the ring buffer.</param>
        /// <param name="bufferSize">number of elements to create within the ring buffer.</param>
        /// <returns>a constructed ring buffer.</returns>
        /// <exception cref="ArgumentException">if bufferSize is less than 1 or not a power of 2</exception>
        public static ValueRingBuffer<T> CreateSingleProducer(Func<T> factory, int bufferSize)
        {
            return CreateSingleProducer(factory, bufferSize, new BlockingWaitStrategy());
        }

        /// <summary>
        /// Create a new ValueRingBuffer with the specified producer type.
        /// </summary>
        /// <param name="producerType">producer type to use <see cref="ProducerType" /></param>
        /// <param name="factory">used to create the events within the ring buffer.</param>
        /// <param name="bufferSize">number of elements to create within the ring buffer.</param>
        /// <param name="waitStrategy">used to determine how to wait for new elements to become available.</param>
        /// <returns>a constructed ring buffer.</returns>
        /// <exception cref="ArgumentOutOfRangeException">if the producer type is invalid</exception>
        /// <exception cref="ArgumentException">if bufferSize is less than 1 or not a power of 2</exception>
        public static ValueRingBuffer<T> Create(ProducerType producerType, Func<T> factory, int bufferSize, IWaitStrategy waitStrategy)
        {
            switch (producerType)
            {
                case ProducerType.Single:
                    return CreateSingleProducer(factory, bufferSize, waitStrategy);
                case ProducerType.Multi:
                    return CreateMultiProducer(factory, bufferSize, waitStrategy);
                default:
                    throw new ArgumentOutOfRangeException(producerType.ToString());
            }
        }

        /// <summary>
        /// Get the event for a given sequence in the RingBuffer.
        /// 
        /// This call has 2 uses.  Firstly use this call when publishing to a ring buffer.
        /// After calling <see cref="RingBuffer.Next()"/> use this call to get hold of the
        /// preallocated event to fill with data before calling <see cref="RingBuffer.Publish(long)"/>.
        /// 
        /// Secondly use this call when consuming data from the ring buffer.  After calling
        /// <see cref="ISequenceBarrier.WaitFor"/> call this method with any value greater than
        /// that your current consumer sequence and less than or equal to the value returned from
        /// the <see cref="ISequenceBarrier.WaitFor"/> method.
        /// </summary>
        /// <param name="sequence">sequence for the event</param>
        /// <returns>the event for the given sequence</returns>
        public ref T this[long sequence]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return ref Util.ReadValue<T>(_entries, _bufferPad + (int)(sequence & _indexMask));
            }
        }
        
        /// <summary>
        /// Sets the cursor to a specific sequence and returns the preallocated entry that is stored there.  This
        /// can cause a data race and should only be done in controlled circumstances, e.g. during initialisation.
        /// </summary>
        /// <param name="sequence">the sequence to claim.</param>
        /// <returns>the preallocated event.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T ClaimAndGetPreallocated(long sequence)
        {
            _sequencer.Claim(sequence);
            return ref this[sequence];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PublishEventScope PublishEvent()
        {
            var sequence = Next();
            return new PublishEventScope(this, sequence);
        }

        /// <summary>
        /// Publishes an event to the ring buffer.  It handles claiming the next sequence, getting the current(uninitialised)
        /// event from the ring buffer and publishing the claimed sequence after translation.
        /// </summary>
        public readonly struct PublishEventScope : IDisposable
        {
            private readonly ValueRingBuffer<T> _ringBuffer;
            private readonly long _sequence;

            public PublishEventScope(ValueRingBuffer<T> ringBuffer, long sequence)
            {
                _ringBuffer = ringBuffer;
                _sequence = sequence;
            }

            public long Sequence => _sequence;

            public ref T Data
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref _ringBuffer[_sequence];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                _ringBuffer.Publish(_sequence);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPublishEvent(out PublishEventScope scope)
        {
            var success = TryNext(out var sequence);
            scope = new PublishEventScope(this, sequence);

            return success;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PublishEventsScope PublishEvents(int count)
        {
            if ((uint)count > _bufferSize)
                ThrowInvalidPublishCountException();

            var endSequence = Next(count);
            return new PublishEventsScope(this, endSequence + 1 - count, endSequence);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPublishEvents(int count, out PublishEventsScope scope)
        {
            if ((uint)count > _bufferSize)
                ThrowInvalidPublishCountException();

            var success = TryNext(count, out var endSequence);
            scope = new PublishEventsScope(this, endSequence + 1 - count, endSequence);

            return success;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ThrowInvalidPublishCountException()
        {
            throw new ArgumentException($"Invalid publish count: It should be >= 0 and <= {_bufferSize}");
        }

        /// <summary>
        /// Publishes an event to the ring buffer.  It handles claiming the next sequence, getting the current(uninitialised)
        /// event from the ring buffer and publishing the claimed sequence after translation.
        /// </summary>
        public readonly struct PublishEventsScope : IDisposable
        {
            private readonly ValueRingBuffer<T> _ringBuffer;
            private readonly long _startSequence;
            private readonly long _endSequence;

            public PublishEventsScope(ValueRingBuffer<T> ringBuffer, long startSequence, long endSequence)
            {
                _ringBuffer = ringBuffer;
                _startSequence = startSequence;
                _endSequence = endSequence;
            }

            public long StartSequence => _startSequence;
            public long EndSequence => _endSequence;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref T Data(int index) => ref _ringBuffer[_startSequence + index];

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                _ringBuffer.Publish(_startSequence, _endSequence);
            }
        }
    }
}
