from PIL import Image, ImageDraw, ImageFont
from pathlib import Path
import math

root = Path(__file__).parent
W,H=1200,750
font = ImageFont.truetype('arialbd.ttf', 42)
small = ImageFont.truetype('arial.ttf', 24)
items = [
 ('event-1.png','RESPONSIBLE COMPUTING','SUMMIT','network'),('event-2.png','COMMUNITY TECH','OUTREACH','people'),
 ('event-3.png','COMPUTING COMMUNITY','ASSEMBLY','people'),('event-4.png','ACCESSIBLE WEB','WORKSHOP','browser'),
 ('event-5.png','CODING PEER','MENTORING','code'),('event-6.png','CYBERSECURITY','CAPTURE THE FLAG','shield'),
 ('event-7.png','DATA SCIENCE','AND AI STUDIO','chart'),('event-8.png','GAME DEVELOPMENT','JAM','game'),
 ('venue-1.png','CAMPUS SPACES','COVERED COURT','court'),('venue-2.png','CAMPUS SPACES','AUDITORIUM','stage'),
 ('venue-3.png','CAMPUS SPACES','AUDIO-VISUAL ROOM','stage'),('venue-4.png','CAMPUS SPACES','LEARNING ROOM','browser'),
 ('venue-cebu.png','EVENT SPACES','COLISEUM','court')]
palettes=[('#102d46','#32c8ab'),('#153b49','#f2bb6c'),('#202f52','#80b9ea'),('#283858','#f3a49b'),('#173a45','#9ad6b0')]
for i,(name,kicker,title,icon) in enumerate(items):
 bg,accent=palettes[i%len(palettes)]; im=Image.new('RGB',(W,H),bg); d=ImageDraw.Draw(im)
 d.ellipse((690,-210,1410,510),fill='#224a5c'); d.ellipse((760,310,1330,880),fill='#193e52')
 for x in range(42,650,35):
  for y in range(55,490,35):
   if (x//35+y//35)%4==0: d.ellipse((x,y,x+3,y+3),fill='#527080')
 d.rounded_rectangle((62,64,258,104),radius=18,fill=accent); d.text((80,72),'CCS  /  DEMO',font=small,fill=bg)
 d.text((64,210),kicker,font=small,fill=accent); d.text((62,258),title,font=font,fill='white',stroke_width=0)
 d.line((64,338,290,338),fill=accent,width=5); d.text((64,370),'ORIGINAL ILLUSTRATION',font=small,fill='#c8d9dc')
 d.rounded_rectangle((720,135,1090,500),radius=38,fill='#f3f5eb'); d.rounded_rectangle((740,155,1070,480),radius=28,fill='#d8e6df')
 dark='#153b49'; a=accent
 if icon in ('browser','code'):
  d.rounded_rectangle((775,210,1035,401),radius=12,fill=dark); d.rectangle((775,210,1035,245),fill=a)
  for j in range(4): d.rounded_rectangle((805,270+j*27,955+(j%2)*42,279+j*27),radius=4,fill='#b6ddce')
 elif icon=='network':
  pts=[(905,300),(805,235),(1008,230),(813,383),(1004,379),(915,420)]
  for p in pts[1:]: d.line((pts[0],p),fill=dark,width=8)
  for x,y in pts: d.ellipse((x-19,y-19,x+19,y+19),fill=a,outline=dark,width=5)
 elif icon=='shield':
  d.polygon([(905,202),(1010,244),(1000,344),(905,417),(810,344),(800,244)],fill=dark)
  d.line([(850,304),(890,344),(966,263)],fill=a,width=14)
 elif icon=='chart':
  d.line([(800,400),(800,225)],fill=dark,width=7); d.line([(800,400),(1023,400)],fill=dark,width=7)
  for j,v in enumerate([70,130,105,180]): d.rounded_rectangle((830+j*48,400-v,862+j*48,400),radius=6,fill=a if j%2 else dark)
 elif icon=='game':
  d.rounded_rectangle((790,255,1020,370),radius=50,fill=dark); d.rectangle((825,300,885,312),fill=a); d.rectangle((849,276,861,336),fill=a)
  for x,y in [(965,290),(987,312),(943,312),(965,334)]: d.ellipse((x-8,y-8,x+8,y+8),fill=a)
 elif icon=='people':
  for x,y in [(840,265),(905,245),(970,265)]:
   d.ellipse((x-25,y-25,x+25,y+25),fill=dark); d.rounded_rectangle((x-37,y+35,x+37,y+115),radius=25,fill=a if x==905 else dark)
 elif icon=='stage':
  d.rectangle((790,215,1020,347),fill=dark); d.rectangle((810,235,1000,327),fill=a); d.rectangle((770,370,1040,390),fill=dark)
  for x in range(800,1030,55): d.ellipse((x,410,x+28,438),fill=dark)
 else:
  d.polygon([(790,395),(840,265),(970,265),(1020,395)],fill=dark); d.line([(815,365),(995,365)],fill=a,width=6)
  d.line([(905,265),(905,395)],fill=a,width=5); d.ellipse((875,310,935,370),outline=a,width=5)
 d.text((64,658),'ATTENDTA   /   CCS CAMPUS LIFE',font=small,fill='#aecac8')
 im.save(root/name,optimize=True)
