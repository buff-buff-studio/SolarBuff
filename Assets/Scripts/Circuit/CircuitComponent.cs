﻿using System;
using System.Collections.Generic;
using NetBuff.Components;
using UnityEngine;

namespace SolarBuff.Circuit
{
    public abstract class CircuitComponent : NetworkBehaviour
    {
        [HideInInspector, SerializeField]
        public List<CircuitPlug> plugs;

        protected virtual void OnEnable()
        {
            OnRefresh();
        }

        protected virtual void OnDisable()
        {
            
        }

        protected virtual void OnDestroy()
        {
            //Destroy all connections
            foreach (var plug in GetPlugs())
            {
                if (plug.Connection == null) continue;
                Destroy(plug.Connection.gameObject);
            }
        }
        
        public virtual T ReadOutput<T>(CircuitPlug plug)
        {
            return default;
        }
        
        protected virtual void OnRefresh()
        {
            
        }

        public void Refresh()
        {
            if (!Application.isPlaying)
                return;
            
            OnRefresh();
            
            //Spread through output ports
            foreach (var plug in GetPlugs())
            {
                if (plug.type != CircuitPlug.Type.Output) continue;
                if (plug.Connection == null) continue;

                plug.Connection.Refresh();
            }
        }

        public static T SafeOutput<T>(bool b)
        {
            switch (Type.GetTypeCode(typeof(T)))
            {
                case TypeCode.Boolean:
                    return (T)(object)b;
                case TypeCode.Single:
                    return (T)(object)(b ? 1f : 0f);
                default:
                    return default;
            }
        }
        
        public static T SafeOutput<T>(float f)
        {
            switch (Type.GetTypeCode(typeof(T)))
            {
                case TypeCode.Boolean:
                    return (T)(object)(f > 0.5f);
                case TypeCode.Single:
                    return (T)(object)f;
                default:
                    return default;
            }
        }

        public IEnumerable<CircuitPlug> GetPlugs()
        {
            return plugs;
        }
    }
}