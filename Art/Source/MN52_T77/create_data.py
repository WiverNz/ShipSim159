"""Estimated passive-vessel data using the shared vessel schema."""
import json
from pathlib import Path
root=Path(__file__).resolve().parents[3]
d=json.loads((root/'Assets/ShipSimulator/Data/Vessels/Serpukhov_R96.json').read_text())
d['nonSelfPropelled']=True
d['identity']={'displayName':'МН-52, Т-77 (1989)','project':'T-77','vesselType':'Non-self-propelled petroleum barge'}
d['dimensions']={'lengthOverallM':28.8,'lengthBetweenPerpendicularsM':27.2,'beamOverallM':6.31,'beamMouldedM':6,'depthMouldedM':1.5,'loadedDraftM':1.2}
d['massProperties'].update(loadedMassKg=153000,lightshipMassKg=52500,deadweightKg=100500,loadFraction=1,loadingCondition='Loaded petroleum barge, estimated full displacement',centerOfMassLocalM={'x':0,'y':-.35,'z':0},rollGyrationRadiusM=2.1,pitchGyrationRadiusM=7.3,yawGyrationRadiusM=7.4)
d['hydrostatics'].update(blockCoefficient=.78125,midshipCoefficient=.97,waterplaneCoefficient=.90,longitudinalCentreOfBuoyancyM=0,stationCount=32,stripsAcross=8,heaveDampingRatio=.5,rollDampingRatio=.35)
# A symmetric inferred section table uses broad bilges and rising ends.
zs=[-14.4,-14,-13,-12,-10,-8,-6,-4,-2,0,2,4,6,8,10,12,13,14,14.4]
stations=[]
for z in zs:
 b=max(0,(abs(z)-10)/4.4)**2*1.05
 w=3-.55*max(0,(abs(z)-12)/2.4)
 stations.append({'localZ':z,'heightsAboveKeelM':[b,b+.15,max(b+.16,1.2),1.5], 'halfBreadthsM':[w*.72,w*.92,w,w]})
# Fit estimated immersed breadths to the two documented displacement/draft pairs.
# This is a hydrostatic inference, not a recovered lines plan; deck breadth stays 6 m.
def volume(rows,draft):
 areas=[]
 for row in rows:
  h=row['heightsAboveKeelM'];w=row['halfBreadthsM'];area=0
  for i in range(len(h)-1):
   top=min(draft,h[i+1])
   if top<=h[i]:continue
   width=w[i]+(w[i+1]-w[i])*(top-h[i])/(h[i+1]-h[i])
   area+=(top-h[i])*(w[i]+width)
  areas.append(area)
 return sum((a+b)*.5*(zb-za) for a,b,za,zb in zip(areas,areas[1:],zs,zs[1:]))
for row in stations:
 h=row['heightsAboveKeelM'];w=row['halfBreadthsM']
 if h[0]<.5 and .5 not in h:
  i=next(i for i in range(len(h)-1) if h[i]<.5<h[i+1]);value=w[i]+(w[i+1]-w[i])*(.5-h[i])/(h[i+1]-h[i]);h.insert(i+1,.5);w.insert(i+1,value)
base=json.loads(json.dumps(stations))
def fitted(low,upper):
 rows=json.loads(json.dumps(base))
 for row in rows:
  for i,h in enumerate(row['heightsAboveKeelM']):
   factor=low if h<=.5 else low+(upper-low)*min(1,(h-.5)/.7) if h<=1.21 else 1
   row['halfBreadthsM'][i]*=factor
 return rows
low=52.5/volume(base,.5)
v0=volume(fitted(low,0),1.2);v1=volume(fitted(low,1),1.2)
upper=(153-v0)/(v1-v0);stations=fitted(low,upper)
print('Hydrostatic breadth factors',low,upper,'volumes',volume(stations,.5),volume(stations,1.2))
d['hydrostatics']['sectionTable']={'enabled':True,'estimated':True,'source':'Photo-derived spoon ends, not shipyard offsets; compare equilibrium against published draft.', 'halfStripsAcross':8,'stripHalfWidthM':3,'tableHeightStepM':.025,'capHeightAboveKeelM':1.5,'minimumMeanDraftM':.35,'maximumMeanDraftM':1.3,'maximumTrimM':.4,'stations':stations}
d['resistance'].update(wettedSurfaceM2=195,formFactor=1.3,residualResistanceCoefficient=.012)
d['hull'].update(surgeAddedMassPrime=.008,swayAddedMassPrime=.20,yawAddedInertiaPrime=.012,derivation='Estimated blunt T-77 barge coefficients. No measured MN-52 resistance or manoeuvring derivatives.')
d['engine']={'engineCount':0,'powerPerEngineW':0,'ratedPropellerRpm':0,'gearEfficiency':0,'shaftEfficiency':0}
d['propeller']={'count':0,'diameterM':0,'longitudinalPositionsM':[],'lateralPositionsM':[]}
d['rudder']={'count':0,'maxAngleDeg':0,'rateDegPerSecond':0,'lateralPositionsM':[]}
d.pop('steeringNozzle',None)
d['windage'].update(frontalAreaM2=17,lateralAreaM2=37,lateralCentroidLongitudinalM=-6,lateralCentroidHeightM=2.5)
d['controlLimits']={'maxLoadedSpeedMps':3,'throttleCommandRatePerSecond':.3,'rudderCommandRatePerSecond':.5}
d['sourceReferences']=['https://fleetphoto.ru/vessel/3081/','https://fleetphoto.ru/projects/335/','https://fleetphoto.ru/photo/89189/','https://fleetphoto.ru/photo/4585/','https://su-zholdary.kz/filialy/semeyskiy-filial/potrebitelyam/harakteristiki-sudov/']
(root/'Assets/ShipSimulator/Data/Vessels/MN52_T77.json').write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n')
