namespace DesktopLife.Core;
public enum CharacterExpression { Speak, WriteNote, DrawDoodle, Purr, Scratch, Knead, LickPaw, HuntCrouch, GenericRest, Observe, Bark, WagTail, Sniff }
public static class CharacterCapability
{
    public static bool Allows(PetAppearance character,CharacterExpression expression)=>expression switch
    {
        CharacterExpression.Speak or CharacterExpression.WriteNote or CharacterExpression.DrawDoodle=>character==PetAppearance.Girl,
        CharacterExpression.Purr or CharacterExpression.Scratch or CharacterExpression.Knead or CharacterExpression.LickPaw or CharacterExpression.HuntCrouch=>character==PetAppearance.Cat,
        CharacterExpression.Bark or CharacterExpression.WagTail or CharacterExpression.Sniff=>character==PetAppearance.BorderCollie,
        _=>Enum.IsDefined(character)&&Enum.IsDefined(expression)
    };
    public static bool Allows(PetAppearance character,BodyAction action)=>Enum.IsDefined(action)&&Enum.IsDefined(character)&&
        (action is not (BodyAction.WriteNote or BodyAction.DrawDoodle)||character==PetAppearance.Girl)&&
        (action!=BodyAction.BatToy||character!=PetAppearance.BorderCollie);
    public static BodyAction Resolve(PetAppearance character,BodyAction intent)=>character!=PetAppearance.Cat&&intent==BodyAction.BatToy?BodyAction.PlayToy:Allows(character,intent)?intent:BodyAction.Explore;
    public static BodyAction Pose(PetAppearance character,BodyAction pose)=>character==PetAppearance.Girl&&pose==BodyAction.Groom?BodyAction.Sit:pose;
    public static string CareDescription(CareKind kind,PetAppearance character=PetAppearance.Cat)=>character==PetAppearance.Girl?kind switch
    {CareKind.Feed=>"她準備找個合適的位置用餐。",CareKind.Pet=>"她很開心你來陪伴。",CareKind.Play=>"她準備挑選一項房間活動。",CareKind.Groom=>"她準備整理頭髮。",_=>"她準備休息。"}:kind switch
    {CareKind.Feed=>"牠正享用你準備的點心。",CareKind.Pet=>"牠感受到你的輕撫。",CareKind.Play=>"你邀牠一起玩球。",CareKind.Groom=>"你輕輕幫牠梳理毛毛。",_=>"你讓牠安心休息。"};
}
