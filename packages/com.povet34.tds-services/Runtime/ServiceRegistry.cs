using System;
using System.Collections.Generic;

namespace TDS.Core
{
    /// <summary>
    /// 타입 기반 서비스 레지스트리 (순수 C#, 테스트 가능).
    /// 흩어진 `Singleton.instance` 직접 결합과 `FindObjectOfType` 스크래핑을 대체하는 시임(Phase 0.3).
    /// 매니저는 자신을 인터페이스로 등록하고, 소비자는 타입으로 해석(resolve)한다.
    /// </summary>
    public class ServiceRegistry
    {
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        public void Register<T>(T service) where T : class
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            _services[typeof(T)] = service;
        }

        public bool TryResolve<T>(out T service) where T : class
        {
            if (_services.TryGetValue(typeof(T), out var obj))
            {
                service = (T)obj;
                return true;
            }
            service = null;
            return false;
        }

        public T Resolve<T>() where T : class
            => TryResolve<T>(out var s) ? s : null;

        public bool IsRegistered<T>() where T : class => _services.ContainsKey(typeof(T));

        public bool Unregister<T>() where T : class => _services.Remove(typeof(T));

        public void Clear() => _services.Clear();

        public int Count => _services.Count;
    }
}
