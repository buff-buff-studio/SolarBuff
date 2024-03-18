﻿using System;
using System.Collections.Generic;
using System.IO;
using ExamplePlatformer;
using NetBuff.Components;
using NetBuff.Interface;
using NetBuff.Misc;
using SolarBuff.Circuit.Components;
using UnityEngine;

namespace SolarBuff.Circuit
{
    [RequireComponent(typeof(LineRenderer))]
    [ExecuteInEditMode]
    public class CircuitPhysicalCable : NetworkBehaviour, ICircuitConnection
    {
        private static bool _isQuitting;
        private static readonly Collider[] _Results = new Collider[10];
        
        [Serializable]
        public struct Node
        {
            public GameObject gameObject;
            public Rigidbody rigidbody;
            public Joint joint;
        }

        [Header("SETTINGS")] 
        public int minNodeCount = 3;
        public int maxNodeCount = 15;
        public float nodeDistance = 0.25f;

        [Header("REFERENCES")]
        public GameObject nodePrefab;
        public GameObject connectorPrefab;
      
        [Header("STATE")]
        public List<Node> nodes = new();
        public Rigidbody helder;
        
        public Node Head => nodes[^1];
        public Node Tail => nodes[0];
        
        public Rigidbody Helder
        {
            get => helder;
            set
            {
                OnDisable();
                
                if(helderJoint != null)
                    Destroy(helderJoint);
                helderJoint = null;
                
                helder = value;
                if (helder != null)
                {
                    helderJoint = helder.gameObject.AddComponent<HingeJoint>();
                    helderJoint.anchor = Vector3.zero;
                    helderJoint.connectedBody = Head.rigidbody;
                }
                
                if(HasAuthority)
                    ServerBroadcastPacket(CreatePacket());

                Refresh();
            }
        }

        [SerializeField]
        private HingeJoint helderJoint;
        
        [HideInInspector, SerializeField]
        private GameObject connector;
        
        private LineRenderer _renderer;
        
        public void OnEnable()
        {
            if (Application.isPlaying)
                _renderer = GetComponent<LineRenderer>();
            
            Refresh();
            
            if (!Application.isPlaying)
                return;
            GetPacketListener<PlayerPunchActionPacket>().OnServerReceive += OnPlayerPunch;
            InvokeRepeating(nameof(TickCable), 0, 0.05f);
        }

        protected virtual void OnDisable()
        {
            if(_isQuitting)
                return;
            
            if(PlugA != null)
            {
                PlugA.Connection = null;
                if(PlugA.Owner != null)
                    PlugA.Owner.Refresh();
            }
            
            if(PlugB != null)
            {
                PlugB.Connection = null;
                if(PlugB.Owner != null)
                    PlugB.Owner.Refresh();
            }
        }
        
        protected virtual void OnApplicationQuit () 
        {
            _isQuitting = true;
        }

        private void OnPlayerPunch(PlayerPunchActionPacket packet, int client)
        {
            var puncher = GetNetworkObject(packet.Id).gameObject.GetComponentInChildren<Rigidbody>();

            if (Helder == puncher)
            {
                Helder = null;

                var transform1 = puncher.transform;
                var size = Physics.OverlapSphereNonAlloc(transform1.position + transform1.forward * 0.5f + new Vector3(0, 0.5f, 0), 1.5f, _Results);
                for (var i = 0; i < size; i++)
                {
                    var hit = _Results[i];
                    var socket = hit.GetComponent<CircuitSocket>();
                    
                    if (socket != null && socket.socket.Connection == null && PlugA != socket.socket)
                    {
                        if(socket.socket.type == PlugA.type)
                            continue;
                        Helder = socket.socket.GetComponentInParent<Rigidbody>();
                        break;
                    }
                }
            }
            else
            {
                //check radius
                if (Vector3.Distance(puncher.transform.position, Head.gameObject.transform.position) > 2f)
                    return;
                
                Helder = puncher;
            }
        }

        public void TickCable()
        {
            if (nodes.Count == 0)
            {
                var pos = transform.position;
                for (var i = 0; i < minNodeCount; i++)
                {
                    _CreateNode(pos, i);
                    pos += Vector3.forward * nodeDistance;
                }
                
                connector = Instantiate(connectorPrefab, Vector3.one, Quaternion.identity, transform);
                Helder = helder;
            }

            if (nodes.Count > minNodeCount || Helder != null)
            {
                if(helderJoint != null)
                {
                    var target = helderJoint.transform.position;
                    
                    for (var i = 1; i < nodes.Count; i++)
                    {
                        var node = nodes[i];
                        var prevNode = nodes[i - 1];

                        var distance = Vector3.Distance(node.gameObject.transform.position,
                            prevNode.gameObject.transform.position);
                        
                        if (distance >= nodeDistance * 2)
                        {
                            if (nodes.Count < maxNodeCount)
                            {
                                var pos = Head.gameObject.transform.position;
                                var p = pos + (target - pos).normalized * nodeDistance;
                                _CreateNode(p, nodes.Count);
                            }
                            
                            break;
                        }
                    }
                    
                    while(nodes.Count < maxNodeCount && Vector3.Distance(Head.gameObject.transform.position, target) >= nodeDistance)
                    {
                        var pos = Head.gameObject.transform.position;
                        var p = pos + (target - pos).normalized * nodeDistance;
                        _CreateNode(p, nodes.Count);
                    }
                }
                
                //Delete nodes if not needed
                for (var i = 1; i < nodes.Count - 1; i++)
                {
                    var next = nodes[i + 1];
                    var prev = nodes[i - 1];

                    var distance = Vector3.Distance(next.gameObject.transform.position,
                        prev.gameObject.transform.position);
                        
                    if (distance < nodeDistance * 2)
                    {
                        next.joint.connectedBody = prev.rigidbody;
                        Destroy(nodes[i].gameObject);
                        nodes.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        private void FixedUpdate()
        { 
            if(Application.isPlaying)
                UpdateVisual();
        }

        private void Update()
        {
            if (!Application.isPlaying)
                return;
            if(nodes.Count < 2)
                return;
            
            var head = Head.gameObject.transform.position;

            if(helder == null)
            {
                var before = nodes[^2].gameObject.transform.position;
                connector.transform.position = head;
                connector.transform.forward = (head - before).normalized;
            }
            else
            {
                var ht = helder.transform;
                var pos = ht.position;
                var fw = ht.forward;
                connector.transform.position = pos;
                connector.transform.forward =-fw;
            }
        }

        public void UpdateVisual()
        {
            _renderer.positionCount = nodes.Count;
            _renderer.SetPositions(nodes.ConvertAll(n => n.gameObject.transform.position).ToArray());
        }

        private void _CreateNode(Vector3 position, int index)
        {
            var go = Instantiate(nodePrefab, position, Quaternion.identity, transform);
            var node = new Node
            {
                gameObject = go,
                rigidbody = go.GetComponent<Rigidbody>(),
                joint = go.GetComponent<SpringJoint>()
            };

            var hasPrev = index < nodes.Count;
            var prevOnPos = hasPrev ? nodes[index] : default;
            
            nodes.Insert(index, node);
            
            if (nodes.Count > 1)
            {
                var prevNode = nodes[index - 1];
                var cj = (node.joint as SpringJoint)!;
                cj.anchor = Vector3.zero;

                if (hasPrev)
                {
                    prevOnPos.joint.connectedBody = node.rigidbody;
                }
                
                cj.autoConfigureConnectedAnchor = false;
                cj.connectedAnchor = new Vector3(0, 0, 0);
                cj.connectedBody = prevNode.rigidbody;
                cj.minDistance = 0;
                cj.maxDistance = nodeDistance;
                cj.spring = 1000f;
                cj.enableCollision = false;
                cj.tolerance = 0;
            }
            else
            {
                node.rigidbody.isKinematic = true;
            }

            if (index != nodes.Count - 1) return;
            if (helderJoint == null) return;
            
            helderJoint.connectedBody = node.rigidbody;
        }

        public bool Refresh()
        {
            if (_RefreshInternal())
            {
                if (PlugA.type == CircuitPlug.Type.Input)
                {
                    PlugB.Owner.Refresh();
                }
                else
                {
                    PlugB.Owner.Refresh();
                }
                
                if(_renderer != null)
                    _renderer.material.color = Color.Lerp(Color.black, Color.red, PlugA.ReadValue<float>());
                return true;
            }
            
            if(_renderer != null)
                _renderer.material.color = Color.black;
            return false;
        }

        public CircuitPlug PlugA
        {
            get => transform.GetComponentInParent<CircuitPlug>();
        }
        
        public CircuitPlug PlugB
        {
            get => helder == null ? null : helder.GetComponent<CircuitPlug>();
        }
        
        private bool _RefreshInternal()
        {
            if (PlugA != null && PlugB != null)
            {
                PlugA.Connection = this;
                PlugB.Connection = this;

                return true;
            }
            
            if (PlugA != null && PlugA.Connection as CircuitPhysicalCable == this)
                PlugA.Connection = null;
            
            if (PlugB != null && PlugB.Connection as CircuitPhysicalCable == this)
                PlugB.Connection = null;
            
            return false;
        }

        #region Networking

        public override void OnClientConnected(int clientId)
        {
            ServerSendPacket(CreatePacket(), clientId, true);
        }

        public override void OnClientReceivePacket(IOwnedPacket packet)
        {
            if(packet is PhysicalCablePacket p)
            {
                if (HasAuthority)
                    return;
                
                Helder = p.Holder == NetworkId.Empty ? null : GetNetworkObject(p.Holder).GetComponentInChildren<Rigidbody>();
                
                while (nodes.Count > p.Nodes.Length)
                {
                    Destroy(nodes[^1].gameObject);
                    nodes.RemoveAt(nodes.Count - 1);
                }
                
                for (var i = 0; i < nodes.Count; i++)
                {
                    nodes[i].gameObject.transform.position = p.Nodes[i];
                }

                while (nodes.Count < p.Nodes.Length)
                {
                    _CreateNode(p.Nodes[nodes.Count], nodes.Count);
                }
                
                Refresh();
            }
        }


        private PhysicalCablePacket CreatePacket()
        {
            return new PhysicalCablePacket
            {
                Id = Id,
                Holder = helder == null ? NetworkId.Empty : helder.GetComponentInParent<NetworkIdentity>().Id,
                Nodes = nodes.ConvertAll(n => n.gameObject.transform.position).ToArray()
            };
        }

        #endregion
    }
    
    public class PhysicalCablePacket : IOwnedPacket
    {
        public NetworkId Id { get; set; }
        public NetworkId Holder { get; set; }
        
        public Vector3[] Nodes { get; set; }
        
        public void Serialize(BinaryWriter writer)
        {
            Id.Serialize(writer);
            Holder.Serialize(writer);
            
            writer.Write(Nodes.Length);
            foreach (var node in Nodes)
            {
                writer.Write(node.x);
                writer.Write(node.y);
                writer.Write(node.z);
            }
        }

        public void Deserialize(BinaryReader reader)
        {
            Id = NetworkId.Read(reader);
            Holder = NetworkId.Read(reader);
            
            var count = reader.ReadInt32();
            Nodes = new Vector3[count];
            for (var i = 0; i < count; i++)
            {
                Nodes[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            }
        }
    }
}