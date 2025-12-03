# WallHitSound

壁に衝突したときに音を鳴らすMODです。  
壁を避けたつもりでも、ぶつかっていることはありませんか？  
私は大体ぶつかっています。  

## 設定画面
<img width="40%" height="40%" alt="image" src="https://github.com/user-attachments/assets/7afa7e6a-bb7b-4fbd-9e5f-b0fb1c529c7d" />  

<small>※画像は開発中のものです。</small> 
- **Enabled**：Modの有効/無効を切り替えます。

- **Volume**：beep音や設定した音のボリュームを変更します。

- **Sound Type**：壁に衝突したときに鳴らしたい音を選択します。  
  ・beepを選択するとbeep音が鳴ります。  
  ・鳴らしたい音を「[BeatSaber]\UserData\WallHitSound」フォルダに格納することでプルダウンから選択可能になります。    
  ・対応音源は、wav、ogg、mp3になります。  
  ・初期音源としてdeep_impact.wav、wall_hit.wavが生成されます。(フォルダ作成時)  

- **Beep Frequency(Hz)**：beep音の周波数を変更します。  
　※Sound Typeでbeepを選択した場合のみ設定が有効になります。  
  <img width="40%" height="40%" alt="image" src="https://github.com/user-attachments/assets/16949e34-562e-4d67-b94a-fd84ceae1573" />

- **Pitch**：beep音や設定した音のピッチを変更します。

- **Collision Particels**（おまけ機能）：壁に衝突時に出す火花のパーティクルを数を設定します。  
                                       ※0(デフォルト値)を設定すると、この機能は無効になります。

- **TEST SOUNDボタン**：設定したサウンドをテストします。

- **RESET ALL SETTINGSボタン**：すべての設定を初期状態にリセットします。
