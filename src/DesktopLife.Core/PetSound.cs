namespace DesktopLife.Core;
public enum PetSound { Meow, Purr, Bell }
public static class PetSoundWave
{
    public static byte[] Create(PetSound sound)
    {
        const int rate=22050;var duration=sound==PetSound.Bell?.7:sound==PetSound.Meow?.85:1.4;
        var count=(int)(rate*duration);using var stream=new MemoryStream();using var writer=new BinaryWriter(stream);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+count*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(rate);writer.Write(rate*2);writer.Write((short)2);writer.Write((short)16);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(count*2);
        var phase=0d;var random=new Random(41);var noise=0d;
        for(var i=0;i<count;i++)
        {
            var t=i/(double)rate;var u=t/duration;var attack=Math.Min(1,t/.03);var release=Math.Min(1,(duration-t)/.12);double sample;
            if(sound==PetSound.Bell)sample=(Math.Sin(2*Math.PI*1350*t)+.45*Math.Sin(2*Math.PI*2197*t)+.2*Math.Sin(2*Math.PI*3611*t))*Math.Exp(-7*t)*attack*.28;
            else if(sound==PetSound.Purr)
            {noise=.7*noise+.3*(random.NextDouble()*2-1);sample=(noise*.55+Math.Sin(2*Math.PI*95*t)*.22)*( .25+.75*Math.Pow(Math.Sin(2*Math.PI*25*t),2))*.5*attack*release;}
            else
            {
                var pitch=260+200*Math.Sin(Math.PI*Math.Min(1,u*1.5))-90*u;phase+=2*Math.PI*pitch/rate;
                sample=(Math.Sin(phase)+.45*Math.Sin(phase*2)+.25*Math.Sin(phase*3)+.12*Math.Sin(phase*5))*.22*attack*release*(.75+.25*Math.Sin(Math.PI*u));
            }
            writer.Write((short)(Math.Clamp(sample,-.95,.95)*short.MaxValue));
        }
        return stream.ToArray();
    }
}
