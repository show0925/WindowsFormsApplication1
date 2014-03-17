using System.Collections.Generic;
using System.Drawing;

namespace GXService.CardRecognize.Contract
{
    public interface ICardsRecognizer
    {
        void Start();

        Rectangle Match(byte[] captureBmpData, byte[] tmplBmpData, float similarityThreshold);

        RecognizeResult Recognize(RecoginizeData data);

        CardTypeResult ParseCardType(List<Card> cards);

        CardTypeResult ParseCardTypeVsEnemy(List<Card> cards, List<Card> cardsEnemy);

        void Stop();
    }

    // 使用下面示例中说明的数据约定将复合类型添加到服务操作。
    public class RecognizeResult
    {
        public List<Card> Result { get; set; }
    }

    public class RecoginizeData
    {
        public byte[] CardsBitmap { get; set; }
    }
}
