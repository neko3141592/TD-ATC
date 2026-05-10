using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ブレーキ制御装置:
/// ノッチ指令から目標ブレーキ力(N)と目標BC圧(kPa)を決める。
/// </summary>
internal class BrakeControlUnit
{
    /// <summary>
    /// 役割: GetTargetTotalBrakeForceN の処理を実行します。
    /// </summary>
    /// <param name="trainSpec">trainSpec を指定します。</param>
    /// <param name="brakeNotch">brakeNotch を指定します。</param>
    /// <param name="massKg">massKg を指定します。</param>
    /// <returns>処理結果を返します。</returns>
    public float GetTargetTotalBrakeForceN(TrainSpec trainSpec, int brakeNotch, float massKg)
    {
        // ノッチ -> 目標減速度を使って、編成全体の必要制動力[N]を作る
        if (trainSpec == null || brakeNotch <= 0)
        {
            return 0f;
        }

        float safeMassKg = Mathf.Max(1f, massKg);
        float targetDecelMS2 = Mathf.Max(0f, trainSpec.GetBrakeDeceleration(brakeNotch));
        return targetDecelMS2 * safeMassKg;
    }

    /// <summary>
    /// 役割: AllocateWithSaturation の処理を実行します。
    /// </summary>
    /// <param name="caps">caps を指定します。</param>
    /// <param name="target">target を指定します。</param>
    /// <returns>処理結果を返します。</returns>
    public float[] AllocateWithSaturation(IReadOnlyList<float> caps, float target)
    {
        // caps[i] の範囲で target を配る（上限の大きい順に貪欲配分）
        // 返り値は「各要素に実際に割り当てた量」
        int count = caps?.Count ?? 0;
        float[] allocated = new float[count];

        if (count == 0 || target <= 0f)
        {
            return allocated;
        }

        List<int> order = new List<int>(count);
        for (int i = 0; i < count; i++)
        {
            order.Add(i);
        }

        // 上限の大きい車両から順に割り当てる（貪欲）
        order.Sort((a, b) => Mathf.Max(0f, caps[b]).CompareTo(Mathf.Max(0f, caps[a])));

        float remain = target;
        for (int i = 0; i < order.Count && remain > 0f; i++)
        {
            int index = order[i];
            float cap = Mathf.Max(0f, caps[index]);
            if (cap <= 0f)
            {
                continue;
            }

            float assigned = Mathf.Min(cap, remain);
            allocated[index] = assigned;
            remain -= assigned;
        }

        return allocated;
    }

    /// <summary>
    /// 役割: AllocateEvenlyWithSaturation の処理を実行します。
    /// </summary>
    /// <param name="caps">caps を指定します。</param>
    /// <param name="target">target を指定します。</param>
    /// <returns>処理結果を返します。</returns>
    public float[] AllocateEvenlyWithSaturation(IReadOnlyList<float> caps, float target)
    {
        // caps[i] の範囲で target をなるべく均等に配る（水位均し）
        int count = caps?.Count ?? 0;
        float[] allocated = new float[count];
        if (count == 0 || target <= 0f)
        {
            return allocated;
        }

        bool[] active = new bool[count];
        int activeCount = 0;
        for (int i = 0; i < count; i++)
        {
            float cap = Mathf.Max(0f, caps[i]);
            if (cap > 0f)
            {
                active[i] = true;
                activeCount++;
            }
        }

        float remain = target;
        const float epsilon = 0.0001f;
        int guard = Mathf.Max(1, count * 4); // 無限ループ防止
        for (int loop = 0; loop < guard && remain > epsilon && activeCount > 0; loop++)
        {
            float share = remain / activeCount;
            bool anyChanged = false;

            for (int i = 0; i < count; i++)
            {
                if (!active[i])
                {
                    continue;
                }

                float cap = Mathf.Max(0f, caps[i]);
                float room = cap - allocated[i];
                if (room <= epsilon)
                {
                    active[i] = false;
                    activeCount--;
                    continue;
                }

                float add = Mathf.Min(share, room);
                if (add > 0f)
                {
                    allocated[i] += add;
                    remain -= add;
                    anyChanged = true;
                }

                if (room - add <= epsilon)
                {
                    active[i] = false;
                    activeCount--;
                }
            }

            if (!anyChanged)
            {
                break;
            }
        }

        return allocated;
    }
}
