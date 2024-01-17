﻿using ExcelDsp.Painter.Extensions;
using System;
using System.Linq;
using UnityEngine;

namespace ExcelDsp.Painter.Tools;

/// <summary>Draws foundation tiles</summary>
internal static class FoundationDrawer
{
    private const float TileRadius = 0.990946f;
    private static Vector3? _start;

    /// <summary>Whether this is currently enabled, overriding the vanilla foundation logic</summary>
    public static bool IsEnabled { get; set; }

    /// <summary>Whether to paint over the shortest or longest path between the selected corners</summary>
    public static bool UseShortestPath { get; set; } = true;

    /// <summary>Update the target world position and tile each frame (when enabled and active) </summary>
    /// <param name="reformTool">Vanilla foundation build tool</param>
    public static void UpdateRaycast(BuildTool_Reform reformTool)
    {
        reformTool.brushSize = 1;
        reformTool.cursorValid = Raycast(reformTool.mouseRay, out Vector3 target);
        SelectRange(reformTool, target, target, true);

        reformTool.controller.cmd = reformTool.controller.cmd with
        {
            test = target,
            target = reformTool.cursorTarget,
            state = reformTool.cursorValid ? 1 : 0,
        };
    }

    /// <summary>Update tool logic each frame (when enabled and active)</summary>
    /// <param name="reformTool">Vanilla foundation build tool</param>
    public static void UpdateAction(BuildTool_Reform reformTool)
    {
        if(VFInput.onGUIOperate)
            return;

        ECursor cursor = reformTool.cursorValid ? ECursor.Reform : ECursor.Ban;
        UICursor.SetCursor(cursor);

        string? info = null;

        if(_start.HasValue)
        {
            if(VFInput.rtsCancel.onDown)
            {
                Reset(reformTool);
                VFInput.UseMouseRight();
            }
            else if(reformTool.cursorValid)
            {
                SelectRange(reformTool, _start.Value, reformTool.cursorTarget, UseShortestPath);

                if(VFInput._buildConfirm.onDown)
                {
                    ApplyReform(reformTool);
                }
                else
                {
                    int indices = reformTool.cursorIndices.Count(i => i > 0);
                    info = $"Selected area: {indices}";
                }
            }
        }
        else if(reformTool.cursorValid && VFInput._buildConfirm.onDown)
        {
            _start = reformTool.cursorTarget;
        }
        else
        {
            info = "Select starting point";
        }

        if(info != null)
            reformTool.actionBuild.model.cursorText = info;
    }

    /// <summary>Reset to initial state</summary>
    public static void Reset(BuildTool_Reform reformTool)
    {
        _start = null;
        UseShortestPath = true;
        Array.Resize(ref reformTool.cursorPoints, 100);
        Array.Resize(ref reformTool.cursorIndices, 100);
    }

    private static void SelectRange(BuildTool_Reform reformTool, Vector3 start, Vector3 end, bool useShortestPath)
    {
        reformTool.cursorPointCount = reformTool.planet.aux.ReformSnapRect(start, end, useShortestPath, ref reformTool.cursorPoints, ref reformTool.cursorIndices, out reformTool.cursorTarget);
        reformTool.reformCenterPoint = reformTool.cursorTarget;
    }

    private static bool Raycast(Ray ray, out Vector3 target)
    {
        target = Vector3.zero;
        if(VFInput.onGUIOperate || !VFInput.inScreen)
            return false;

        const int layerMask = 528;
        bool hitGround = Physics.Raycast(ray, out RaycastHit hitInfo, 400f, layerMask, QueryTriggerInteraction.Collide);

        if(!hitGround)
        {
            Ray inverseRay = new(ray.GetPoint(200f), -ray.direction);
            hitGround = Physics.Raycast(inverseRay, out hitInfo, 200f, layerMask, QueryTriggerInteraction.Collide);
        }

        if(!hitGround)
            return false;

        target = hitInfo.point;
        return true;
    }

    private static void ApplyReform(BuildTool_Reform reformTool)
    {
        if(!ConsumeFoundation(reformTool))
            return;

        VFAudio.Create("reform-terrain", null, reformTool.reformCenterPoint, true, 4);
        FlattenTerrain(reformTool);
        SetReformProps(reformTool);
        Reset(reformTool);
    }

    private static void FlattenTerrain(BuildTool_Reform reformTool)
    {
        reformTool.factory.BeginFlattenTerrain();
        Bounds tileBounds = new(Vector3.zero, Vector3.one * TileRadius * 2);

        for(int i = 0; i < reformTool.cursorPointCount; i++)
        {
            Vector3 cursorPoint = reformTool.cursorPoints[i];
            reformTool.factory.FlattenTerrain(cursorPoint, Quaternion.identity, tileBounds, lift: true, autoRefresh: false);
        }

        reformTool.factory.EndFlattenTerrain(true);
    }

    private static void SetReformProps(BuildTool_Reform reformTool)
    {
        PlatformSystem platformSystem = reformTool.factory.platformSystem;
        foreach(int cursorIndex in reformTool.cursorIndices)
        {
            if(cursorIndex < 0)
                continue;

            int reformType = platformSystem.GetReformType(cursorIndex);
            int reformColor = platformSystem.GetReformColor(cursorIndex);
            if(reformType != reformTool.brushType || reformColor != reformTool.brushColor)
            {
                platformSystem.SetReformType(cursorIndex, reformTool.brushType);
                platformSystem.SetReformColor(cursorIndex, reformTool.brushColor);
            }
        }
    }

    private static bool ConsumeFoundation(BuildTool_Reform reformTool)
    {
        Player player = reformTool.player;
        int itemId = reformTool.handItem.ID;
        int availableItems = player.package.GetItemCount(itemId) + player.inhandItemCount;
        int neededItems = reformTool.cursorPointCount;

        if(availableItems < neededItems)
        {
            UIRealtimeTip.Popup("物品不足".Translate(), true, 1); // Lack of item
            return false;
        }

        int[] consumeRegister = GameMain.statistics.production.factoryStatPool[reformTool.factory.index].consumeRegister;
        consumeRegister[itemId] += neededItems;

        GameMain.gameScenario?.NotifyOnBuild(reformTool.planet.id, itemId, 0);
        GameMain.history.MarkItemBuilt(itemId, neededItems);

        if(player.inhandItemCount > 0)
        {
            int usedCount = Math.Min(neededItems, player.inhandItemCount);
            player.UseHandItems(usedCount, out int _);
            neededItems -= usedCount;
        }

        player.package.TakeTailItems(ref itemId, ref neededItems, out int _);
        return true;
    }
}
