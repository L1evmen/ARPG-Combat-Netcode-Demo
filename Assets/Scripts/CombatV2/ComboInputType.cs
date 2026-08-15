namespace CombatV2
{
    /// <summary>
    /// 输入类型枚举 —— 用于输入缓冲与招式切换判定。
    /// </summary>
    public enum ComboInputType
    {
        Light,   // 轻击（鼠标左键）
        Heavy,   // 重击（鼠标右键，含蓄力释放）
        Up,      // 上方向（W / ↑，配合重击发动升龙斩）
        Dodge    // 闪避（左Shift）
    }
}
