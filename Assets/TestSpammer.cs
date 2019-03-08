using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class TestSpammer : NetworkBehaviour
{

    private float nextUpdateTime = 0f;
    
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(Time.time >= nextUpdateTime)
        {
            nextUpdateTime += 3f;
            SendOutMessages();
        }
    }

    private void SendOutMessages()
    {
        if(isServer)
        {
            RpcMessageOverReliableChannel();
            RpcMessageOverUnreliableChannel();
            RpcMessageOverUnreliableSequencedChannel();
            RpcMessageOverUnreliableFragmentedChannel();
        }
    }

    [ClientRpc]
    private void RpcMessageOverReliableChannel()
    {
        Debug.Log($"{Time.time} Got a ClientRpc over Reliable Channel.");
    }

    [ClientRpc(channel = 1)]
    private void RpcMessageOverUnreliableChannel()
    {
        Debug.Log($"{Time.time} Got a ClientRpc over Unreliable Channel.");
    }

    [ClientRpc(channel = 2)]
    private void RpcMessageOverUnreliableSequencedChannel()
    {
        Debug.Log($"{Time.time} Got a ClientRpc over Unreliable Sequenced Channel.");
    }

    [ClientRpc(channel = 3)]
    private void RpcMessageOverUnreliableFragmentedChannel()
    {
        Debug.Log($"{Time.time} Got a ClientRpc over Unreliable Fragmented Channel.");
    }
}
