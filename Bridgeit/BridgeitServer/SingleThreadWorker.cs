using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace BridgeitServer
{
    interface ISingleThreadHandler<in TInput>
    {
        void Handle(TInput item);
        void OnStop();
        Action<Action> Send { get; set; }
    }

    /// <summary>
    /// Однопоточно обрабатывает поступающие элементы.
    /// Асинхронно запускает поступающие задачи (чисто для порядка)
    /// </summary>
    /// <typeparam name="TInput">Тип обрабатываемых элементов</typeparam>
    class SingleThreadWorker<TInput>
    {
        readonly ConcurrentQueue<TInput> _input = new ConcurrentQueue<TInput>();
        readonly ISingleThreadHandler<TInput> _handler;

        private volatile bool _isStop;

        public SingleThreadWorker(ISingleThreadHandler<TInput> handler)
        {
            _handler = handler;
            handler.Send = Send;
        }

        public void Start()
        {
            new Thread(Loop).Start();
        }

        public void Stop()
        {
            _isStop = true;
        }

        public void Put(TInput item)
        {
            _input.Enqueue(item);
        }

        private void Loop()
        {
            while (!_isStop)
            {
                TInput __item;
                while (_input.TryDequeue(out __item))
                    _handler.Handle(__item);

                Thread.Sleep(1);
            }
            _handler.OnStop();
        }


        private void Send(Action item)
        {
            Task.Factory.StartNew(item);
        }

    }
}
