# Stockfish-GUI
Fairy Stockfish 엔진을 연동시킨 장기 GUI 프로그램입니다.

## 기능
- ```새 게임``` GUI 보드를 초기화합니다.
- ```실시간 불러오기``` 블루스택 장기 for 카카오의 화면을 가져와 GUI를 실시간으로 업데이트합니다.
- ```분석 모드``` 페어리 스톡피시 엔진을 실행합니다.
- ```색상 변경``` 초/한의 색상을 변경합니다.
- ```무르기``` 한 수 무릅니다. 무르기는 기보에 저장되지 않습니다.

- 옵션에서 저장된 Fairy Stockfish Settings 파일을 불러올 수 있습니다.
- 말을 클릭하고 원하는 지점을 클릭하거나 드래그하여 이동할 수 있습니다.
- Alt를 누른 채 장기말을 선택하고 드래그하면 해당 위치로 편집할 수 있습니다. 기보에 저장되지 않습니다.
- 우측 스크롤바를 이용해 이전의 기보로 되돌아 갈 수 있습니다.
  
## Settings

- ```MultiPV``` 이 개수 만큼 최적의 수를 계산하고 화살표로 보여집니다.
- ```Thread``` 계산에 사용될 CPU 스레드의 개수입니다.
- ```Hash``` 계산에 사용될 메모리 크기(Mb)입니다. 
- ```EvalFile``` 사용되는 Fairy Stockfish의 nnue 파일 이름입니다. Fairy Stockfish 장기 변형 버전에 따라 다를 수 잇습니다.
  
- ```Search_Depth``` 최대 탐색 깊이 입니다. 0일 경우에 무제한으로 탐색합니다.
  
  ```0 (20-30)``` 사람이 이길 수 없습니다. 깊은 수 읽기로 알 기 어려운 수들을 종종 둡니다.
  
  ```5``` 9단을 상대로 대략 70% 승률의 실력입니다. 손해보는 수들을 종종 둡니다.
  
  ```7``` 장기 프로기사들을 상대로 대략 80% 승률의 실력입니다. 깊은 수의 차 덫에 종종 당하기도 합니다.
  
나머지 세팅값은 이 GUI에서 사용되지 않거나 필요하지 않습니다.

## 주의사항
- 실시간 불러오기를 진행 중에 착수 속도가 빠를 경우나 특정 돌에 인식이 안되는 경우가 있습니다.

  블루스택 앱 플레이어의 설정에서 성능 탭-높은 프레임 속도 활성화하고 프레임 속도를 240으로 설정해줍니다.
  
  실시간 불러오기가 적용되지 않는다면 화면 창의 크기를 조절하거나 다시 실시간 불러오기(Ctrl+Space)를 선택해주세요. 

- 분석 모드를 진행 중에 장군인 상황에서 말들의 위치를 이동 시키거나 편집할 경우에 강제로 종료되는 문제가 있습니다.
