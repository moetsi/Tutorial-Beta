using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.NetCode;
using Unity.Jobs;
using Unity.Collections;
using UnityEngine.XR;
using Unity.Physics;

[UpdateInGroup(typeof(GhostInputSystemGroup))]
public partial class ARInputSystem : SystemBase
{
    //We will need a command buffer for structural changes
    private BeginSimulationEntityCommandBufferSystem m_BeginSimEcb;
    //We will grab the ClientSimulationSystemGroup because we require its tick in the ICommandData
    private ClientSimulationSystemGroup m_ClientSimulationSystemGroup;

    protected override void OnCreate()
    {
        //We set our variables
        m_ClientSimulationSystemGroup = World.GetOrCreateSystem<ClientSimulationSystemGroup>();
        m_BeginSimEcb = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
        //We will only run this system if the player is in game and if the palyer is an AR player
        RequireSingletonForUpdate<NetworkStreamInGame>();
        RequireSingletonForUpdate<IsARPlayerComponent>();
    }

    protected override void OnUpdate()
    {
        //The only inputs is for shooting or for self destruction
        //Movement will be through the ARPoseComponent
        byte selfDestruct, shoot;
        selfDestruct = shoot = 0;

        //More than 2 touches will register as self-destruct
        if (Input.touchCount > 2)
        {
            selfDestruct = 1;
        }
        //A single touch will register as shoot
        if (Input.touchCount == 1)
        {
            shoot = 1;
        }

        //We grab the AR pose to send to the server for movement
        var arPoseDriver = GetSingleton<ARPoseComponent>();
        //We must declare our local variables before the .ForEach()
        var commandBuffer = m_BeginSimEcb.CreateCommandBuffer();
        var inputFromEntity = GetBufferFromEntity<PlayerCommand>();
        var inputTargetTick = m_ClientSimulationSystemGroup.ServerTick;

        TryGetSingletonEntity<PlayerCommand>(out var targetEntity);
        Job.WithCode(() => {
        if (targetEntity == Entity.Null)
        {
            if (shoot != 0)
            {
                var req = commandBuffer.CreateEntity();
                commandBuffer.AddComponent<PlayerSpawnRequestRpc>(req);
                commandBuffer.AddComponent(req, new SendRpcCommandRequestComponent());
            }
        }
        else
        {
            var input = inputFromEntity[targetEntity];
            input.AddCommandData(new PlayerCommand{Tick = inputTargetTick,
            selfDestruct = selfDestruct, shoot = shoot,
            isAR = 1,
            arTranslation = arPoseDriver.translation.Value,
            arRotation = arPoseDriver.rotation.Value});

        }
        }).Schedule();
        
        //We need to add the jobs dependency to the command buffer
        m_BeginSimEcb.AddJobHandleForProducer(Dependency);
    }
}