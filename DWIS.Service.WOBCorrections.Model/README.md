# Hook-Load and Weight-on-Bit Calibration Framework

*(Surface-Downhole Hybrid Data-Driven Model)*

---

## Problem to be Solved

When drilling, the **surface weight-on-bit** based on the difference between the measured hook-load and a reference hook-load value known to be made without the bit in contact with the formation does not equal the true axial force acting at the bit (Weight-on-Bit, WOB).

Similarly, the **downhole weight-on-bit** based on the measured tension close to the bit and subtracting a tare that is estimated in conditions for which the bit is expected to be not touching the formation is not necessarily the true axial force caused by the reaction of the bit and the formation. 

In both cases, the measured tension contains contributions from:

* hydrostatic forces
* hydraulic pressure imbalance at the bit
* nozzle momentum (jet thrust)
* distributed hydraulic drag along the string
* mechanical sensor-location artifacts (e.g., instrumented sub at top-drive, load pins at top of top-drive, deadline on a draw-works hoisting)

Therefore the measured signal must be decomposed:

$$
T_{measured} = T_{baseline} + F_{hydraulics} + F_{sensor} + F_{bit} 
$$

Goal:

$$
F_{bit} = \text{true WOB}
$$

and we want to estimate it from surface and downhole measurements under changing flowrate and hoisting position.

---

## Forces Acting on the Bit and Measured by a Downhole Tension Sensor (Off-Bottom Reference State)

Off-bottom is the calibration state:
the bit carries **no formation load**.

$$
F_{WOB} = 0
$$

If the rotational speed is sufficiently large compared to the axial speed, mechanical friction transfers mostly into torque and mechanical drag forces are negligeable, hence all measured tension originates from hydraulics and string weight.

---
### Weight
The gravitational force on the portion of pipe below the tension measurement point is:

$$
\vec{F}_g = \rho_s \left( V_o - V_i\right)\ \vec{g}
$$
where 
* $\vec{F}_g$ is the gravitational force on the portion of pipe below the tension measurement, 
* $\rho_s$ is the material density, 
* $V_o$ is the outer volume,
* $V_i$ is the inner volume.

Its axial component is:
$$
\vec{F}_g . \hat{\mathbf{t}} = \rho_s \left( V_o - V_i\right)\ g \cos\theta
$$
where 
* $\hat{\mathbf{t}}$ is the tangent unit vector at the level of the tension measurement,
* $\theta$ is the inclination.

---

### Buoyancy

The buoyancy force applied on the portion of pipe below the tension measurement is:

$$
\vec{F}_b = -\left(\rho_o V_o - \rho_i V_i\right)\ \vec{g} - \left( p_{go}\,A_{o} - p_{gi}\,A_{i} \right)\,\hat{\mathbf{t}}
$$

where 
* $\vec{F}_b$ is the buoyancy force, 
* $\rho_o$ is the fluid density in the annulus, 
* $\rho_i$ is the fluid density inside the string,
* $\vec{g}$ is the gravitational accerelation vector, 
* $p_{go}$ is the hydrostatic pressure in the annulus, 
* $p_{gi}$ is the hydrostatic pressure inside the string, 
* $A_{o}$ is the outer cross-section area at the level of the tension measurement,
* $A_{i}$ is the inner cross-section area.

The term $- \left( p_{go}\,A_{o} - p_{gi}\,A_{i} \right)\,\hat{\mathbf{t}}$ corresponds to the necessary correction of the volumetric buoyancy term, i.e., $-\left(\rho_o V_o - \rho_i V_i\right)\ \vec{g}$ when one side of the portion of drill-string is not exposed to the pressure of the fluid.

The axial component of the buoyancy force is therefore:

$$
\vec{F}_b . \hat{\mathbf{t}} = -\rho \left( V_o - V_i\right) g \cos\theta - p_{g} \left( A_{o}- A_{i} \right)
$$
considering that $\rho_o = \rho_i = \rho$ and $p_{go}=p_{gi}=p_{g} \propto \rho g h_{p}$ where $h_{p}$ is the vertical height of the liquid column above the position of the downhole measurement. The equation can be rewritten as:

$$
\vec{F}_b . \hat{\mathbf{t}} = -\rho \left( V_o - V_i\right) g \cos\theta - \rho g h_{p} \left( A_{o}- A_{i} \right)
$$
---

### Hydrostatic Pressure Imbalance

In hydrodynamic conditions, on the face of the bit, the effect of the pressure differential between the interior of the bit and the borehole acts in the axial direction and can be expressed as:

$$
F_{\Delta p} = A_{eff} (p_i - p_a)
$$

where
* $A_{eff}$ is an effective area for the application of the pressure imbalance,
* $p_i$ is the internal pressure at the bit,
* $p_a$ is the annulus pressure at the bit,

Note that $p_i$ and $p_a$ must be measured at the same depth and must uses the same reference, here absolute pressure. This effect does not concern buoyancy because the hydrotstatic component of pressure is identical on the internal and annulus side and therefore cancels out.

---

### Jet Reaction (Nozzle Momentum)

The mass flowrate is:

$$
\dot m = \rho Q
$$

where
* $\dot m$ is the mass flow rate
* $Q$ is the volumetric flow rate

The jet velocity is:

$$
v = \frac{Q}{A_{TFA}}
$$

where 
* $A_{TFA}$ is the total flow area.

Therefore, the momentum force is:

$$
F_{jet} = \dot m v = \rho \frac{Q^2}{A_{TFA}}
$$

It is directed upward, i.e., it reduces apparent WOB.

---

### Distributed Hydraulic Drag

The wall shear stress and the forces generated by pressure drop across changes of diameters produce an axial drag that is proportional to the fluid density, the length of the section below the downhole tension measurement position and the square of the volumetric flowrate:

$$
F_{drag} = C_d \rho l_{p} Q^2
$$

where 
* $C_d$ is a proportionality coefficient for this viscous related force
* $l_{p}$ is the distance between the downhole sensor and the bit.

### Final Off-bottom Model at the Level of the Downhole Tension Sensor
Combining all contributions, we obtain:

$$
T_{\text{BHA,off}} = \left( \rho_s -\rho \right) \left( V_o - V_i\right)\ g \cos\theta - \rho g h_{p} \left( A_{o}- A_{i} \right) + A_{eff} (p_i - p_a) + \rho \frac{Q^2}{A_{TFA}}  + C_d \rho l_{p} Q^2
$$

Note that the term $A_{eff} (p_i - p_a)$ corresponds to the pressure imbalance force and does not contain hydrostatic pressure effects, while the tem $- \rho g h_{p} \left( A_{o}- A_{i} \right)$ is related to the correction of the buoyancy force when using its volumetric expression and when one side of the portion of drill-string is not exposed to the fluid pressure (it is based on hydrostatic pressure).

## Tension Measured at the Top-side
Again, considering that the rotational speed is sufficiently large compared to the axial velocity, the mechanical friction forces result in mostly torque and the drag forces are negligible. The axial force measured at the top of the string is composed of the buoyed weight, the force at bit and the hydraulic forces along the string induced by circulation.

### Buoyed Weight
When considering the whole drill-string and when mechanical drag effects can be neglected, the Archimedes' principle can be used because the surface is closed and the Gauss theorem can be applied.

$$
F_{bw}=F_w \left(1 - \frac{\rho}{\rho_s}\right)
$$

where
* $F_w$ is the gravitational weight of the string in the actual geometrical conditions given by the trajectory.

$F_w$ is proportional to the vertical height ($h$) of the drill-string, i.e., $F_w \propto h$.

### Distributed Hydraulic Drag

The wall shear stress and pressure drop across changes of diameters like tool-joints and along a tapered string produce an axial drag that is proportional to the fluid density, the length of the string and the square of the volumetric flowrate:

$$
F_{drag} = C_m \rho l Q^2
$$

where
* $C_m$ is a proportionality coefficient for viscous related forces
* $l$ is the length of the string

---

### Forces at the bit
There are two direct force contributions at the bit: $F_{jet}$ and $F_{\Delta p}$.


### Final Off-Bottom Reference Model

Combining all contributions:

$$
T_{\text{top-side,off}} = C_w h \left(1 - \frac{\rho}{\rho_s}\right) + C_m \rho l Q^2 + A_{eff} (p_i - p_a) + \rho  \frac{Q^2}{A_{TFA}} 
$$

where
* $C_w$ is the proportionality coefficient for the weight contribution.

As indicated above, $p_i$ and $p_a$ must be taken at the same depth and must use the same reference, i.e., typically absolute pressure. The depth consistency is important to ensure that buoyancy effects are not accounted two times.

This equation can be regrouped in the following terms:

$$
T_{\text{top-side,off}} = C_w h  - C_w h \frac{\rho}{\rho_s} + A_{eff} (p_i - p_a) + \rho  \frac{Q^2}{A_{TFA}} + C_m \rho l Q^2  
$$


This equation represents the tension that should be measured when the bit is free rotating in the fluid.
During calibration, parameters are fitted so that this predicted tension matches the measured tension in confirmed off-bottom intervals.

Later, when the bit contacts the formation, any additional force beyond this prediction is interpreted as the true Weight-on-Bit.

---

## Surface Hook-Load Measurement Artifacts
There are several possible locations for the tension measurement sensors at the top side:
- on sub or iBOP connected to the top-drive quill,
- on load pins at the top of the top-drive,
- on the dead-line of drawwork based hoisting system.

Each of those measurement may be subject to measurement artifacts.

---

### Instrumented Sub Connected to the Top-drive Quill

Measures closest to true string tension:

$$
T_d = T_{true} + b_d
$$

where 
* $b_d$ is a measurement bias.

---

### Top-Drive Load Pins

Affected by hoses/umbilical pull dependent on height and flow:

$$
T_p = T_{true} + f_p(z,Q)
$$

Model:

$$
f_p(z,Q) = c_0 + c_1 z + c_2 z^2 + c_3 Q + c_4 Q^2 + c_5 zQ
$$

---

### Deadline Measurement

Affected by pulley geometry and friction:

$$
T_{dl} = T_{\text{true}} + f_{dl}(z,\dot z)
$$

$$
f_{dl}(z,\dot z) = d_0 + d_1 z + d_2 . \mathrm{sign}(\dot z)
$$

---

## Available Measurements

### Surface Measurements

* Hook-load ($T_d$ instrumented sub, $T_p$ load pins, $T_{dl}$ deadline)
* Block position $z$
* Flowrate $Q$
* Fluid density $\rho$
* TVD at bit $h$
* TVD at downhole sensor position $h_{p}$
* Inclination for string below the downhole sensor position $\theta$
* drill-string length $l$ (or in other words bit depth)
* Hole depth $s_{hole}$ (for on bottom determination)

### Downhole Measurements

* Tension $T_{BHA}$
* Internal pressure $p_i$
* Annulus pressure $p_a$
* Downhole angular velocity $\omega$ (for sufficient rotation conditions)

---

## Data-Driven Calibration Model
To minimize the number of inputs to the model, group of physical quantities that can be assumed relatively constant during a run, are simply represented by a calibration parameter.

## Calibration of Downhole WOB
During **off-bottom rotation**, the parameters $\alpha_0$, $\alpha_1$, $\alpha_2$, $\alpha_3$ and $\alpha_4$ are calibrated using:

$$
T_{\text{BHA,off}} = \alpha_0 \cos \theta + \alpha_1 \rho \cos \theta + \alpha_2 \rho h_{p} + \alpha_3 (p_i - p_a) + \alpha_4 \rho Q^2 
$$

The calibration uses recursive least squares (RLS).

Note that if there are not enough variations of the density, the terms in $\alpha_0$ and $\alpha_1$ are undistinguishable. In this case the calibration does not account for the term $\alpha_1$. Similarly, if there is not been enough variations of the density, the terms $\alpha_3 (p_i - p_a)$ and $\alpha_4 \rho Q^2$ may be undistinguishable and therefore the calibration should collapse them into one term. Another reason for collapsing these two terms is when there has been little variations of the flowrate. Also not enough variations of the vertical depth and/or inclination may make it difficult to differentiate $\alpha_0$, $\alpha_1$ and $\alpha_2$. All in all, before calibration, an analysis of the sensitivity of the observations to the variations of density, inclination, vertical depth and flowrate is made to determine which terms of the calibration shall be retained.

An uncertainty of the model $T_{\text{BHA,off}}$ is also calculated based on differences between the calibrated model predictions for each of the data in the series used to calibrate the model.

## Calibration of Surface WOB
During **off-bottom rotation**, and considering that the weight is proportional to the vertical height of the string, we fit the parameters $\beta_0$, $\beta_1$, $\beta_2$, $\beta_3$ and $\beta_4$ using RLS in

$$
T_{top-side, off} = \beta_0 h + \beta_1 \rho h + \beta_2 (p_i-p_a) + \beta_3 \rho Q^2 + \beta_4  \rho l Q^2
$$

As above, too little variations of density, vertical depth, length of the drill-string and flowrate may impact the ability to calibrate the model. The sensitivity of the observations to these variations is therefore performed prior to calibration and the terms of the model that are not well separated with regards to the measurements are collapsed.

An uncertainty of the model $T_{top-side, off}$ is also calculated based on differences between the calibrated model predictions for each of the data in the series used to calibrate the model.

Simultaneously the sensor artifacts are calibrated. 

When the drill-string is disconnected to the hoisting system and there is movement of the traveling block, it is then possible to calibrate $b_d$ because $T_{true}=0$ and therefore:

$$
b_d = T_d
$$

An uncertainty of $b_d$ is calculated based on its variations with regards to the series of measurements made in unconnected conditions.

In unconnected conditions, i.e., $T_{true}=0$, it is also possible to calibrate the parameters of the tension at the deadline because it only depends on $z$ and $\mathrm{sign}(\dot z)$:

$$
d_0 + d_1 z + d_2 . \mathrm{sign}(\dot z) = T_{dl} 
$$

For the case of the tension measured at the load pins at the top of the drive, with an empty block, i.e., $T_{true}=0$, only $z$ varies but $Q$ is always zero. That allows to calibrate only certain parameters of the model:

$$
c_0 + c_1 z + c_2 z^2 = T_p
$$

The unconnected condition is determined by the measured tension being lower than a threshold. The threshold is typically defined as a factor ($f_{slips}$) greater than 1 times the constant term of the correction model, i.e., $c_0$ for the tension measured at the load pin, $d_0$ for the tension measured at the deadline and $b_d$ for the tension measured at the instrumented sub. As these values are unknown in initial conditions, a search for a jump ($\Delta T_{slips}$) over a short $z$ displacement ($d_{slips}) in measured tension is used to estimate initial values for these constants. A jump is such that when the block position moves in one direction, the measured tension changes quickly, either dropping when moving downward or increasing when moving upward. The initial value is the smallest observed after a jump. After the initial values have been estimated the first time, this initial estimation process is not used anymore as the standard model calibration for the measurement artifacts can be used.

The surface sensor measurement artifact models for the load pin and the deadline can also be further refined by using measurements made with a drill-string connected to the hoisting system.

If there is an instrumented sub measurement, the following equations are used:

$$
T_p - T_d = f_p(z,Q)
$$
$$
T_{dl} - T_d = f_{dl}(z,\dot z)
$$

Otherwise, if there are downhole tension measurements, the following equations are used:
$$
T_p - T_{BHA} - \gamma_1 h_{p} - \gamma_2 \rho h_{p} - \gamma_3 \rho (l - l_{p}) Q^2 = f_p(z,Q)
$$
$$
T_{dl} - T_{BHA} - \gamma_1 h_{p} - \gamma_2 \rho h_{p}  - \gamma_3 \rho (l - l_{p}) Q^2= f_{dl}(z,\dot z)
$$

where
* $\gamma_1$ corresponds to the calibration of weight and buoyancy  forces that are proportional to the vertical length of the drillsting above the downhole tension measurement sensor.
* $\gamma_2$ corresponds to the calibration of weight and buoyancy  forces that are proportional to the product of density and the vertical length of the drillstring above the downhole tension measurement sensor.
* $\gamma_3$ corresponds to the calibration of viscous pressure forces on the portion of drill-string above the tension measurement sensor.

Uncertainties for the models $f_{dl}(z,\dot z)$ and $f_p(z,Q)$ are also calculated based on the differences between the predicted values and the observed values used for the calibration including both the unconnected but also when the drill-string is attached to the hoisting system.

This allows also to determine the parameters of the flowrate depend contributions in the load pin tension measurement model, i.e., $c_3$, $c_4$ and $c_5$.

Note that if there are both measurements from the load pins and from the deadline, both models are calibrated simultaneously because they share the same terms in $\gamma_1$, $\gamma_2$ and $\gamma_3$. If there is in addition a tension measured at an instrumented sub together with downhole tension measurements, then the terms $\gamma_1$, $\gamma_2$ and $\gamma_3$ are first calibrated using:

$$
T_d - b_d - T_{BHA} = \gamma_1 h_{p} + \gamma_2 \rho h_{p} + \gamma_3 \rho (l - l_{p}) Q^2
$$

As described above, the calibration of the parameters $c_0$, $c_1$, $c_2$, $c_4$, $c_5$, $d_0$, $d_1$, $d_2$, $\gamma_1$, $\gamma_2$ and $\gamma_3$ are preconditioned to an analysis of their sensitivity to the variability of $z$, $\mathrm{sign}(\dot z)$, $Q$, $h$ and $l$ (depending on which of those are relevant for the concerned model).

An uncertainty of the model based on $\gamma_1$, $\gamma_2$ and $\gamma_3$ is also calculated using the differences between the predicted values and the measurements used to calibrate the model.

---

## Required Additional Data

The model requires:

| Type                                                    | Purpose                                                       |
| --------------------------------------------------------| --------------------------------------------------------------|
| distance of pressure measurement ($l_{p}$)              | scaling of viscous pressure forces                            |
| hole depth                                              | detect on-bottom                                              |
| Downhole angular velocity                               | ensure mechanical drag negligible                             |
| Factor for detecting unconnected conditions $f_{slips}$ | in-slips detection                                            |
| Minimum change of hook-load $\Delta T_{slips}$          | in-slips detection                                            |
| Displacement for jump detection $d_{slips}$             | in-slips detection                                            |

---

## Correcting Downhole Weight-On-Bit

After calibration:

$$
T_{DWOB} = T_{BHA} - \alpha_0 \cos \theta - \alpha_1 \rho \cos \theta - \alpha_2 \rho h_{p} - \alpha_3 (p_i - p_a) - \alpha_4 \rho Q^2
$$

## Correcting Surface tension and Weight-On-Bit

After calibration:

$$
T_{corr} = T_{measured} - f_{sensor}(z,Q,\dot z)
$$

And the corrected surface WOB is:

$$
F_{SWOB1} = T_{corr} - \beta_0 h - \beta_1 \rho h - \beta_2 (p_i-p_a) - \beta_3 \rho Q^2 - \beta_4 \rho l Q^2
$$

Alternatively, if there is a downhole tension that is measured and transmitted by high speed telementry, i.e., updated within less than 2 or 3s, and the model parameters $\gamma_1$, $\gamma_2$ and $\gamma_3$ are calibrated, the surface downhole WOB can be corrected using:

$$
F_{SWOB2} = T_{corr} - \gamma_1 h_{p} - \gamma_2 \rho h_{p} - \gamma_3 \rho (l - l_{p}) Q^2 - \alpha_0 \cos \theta - \alpha_1 \rho \cos \theta - \alpha_2 \rho h_{p} - \alpha_3 (p_i - p_a) - \alpha_4 \rho Q^2
$$

When both $F_{SWOB1}$ and $F_{SWOB2}$ are evaluated, the final surface WOB is calculated using sensor fusion. The sensor fusion simply uses the the two estimated values and their uncertainties to assess the most likely value value that would fit with the probability distributions from the two estimations. It also estimates its associated uncertainty.

Properties:

* invariant to flowrate changes
* invariant to block movement
* invariant to sensor type

---

## Handling Mixed Sampling Rates

Surface: 1-10 Hz
Downhole: 10-60 s with mud pulse telemetry and 2-3 s with high speed telemetry.

---

### Window Synchronization Algorithm

1. Buffer continuous surface data
2. When a downhole sample arrives at time $t_k$:

$$
window = [t_k-\Delta t,; t_k]
$$

3. Compute robust medians:
$$
   \tilde{x} = \mathrm{median}(x(t))
$$

4. Compute motion direction:

5. Reject unstable windows using MAD filters

6. If off-bottom & RPM high -> update calibration

7. Always compute corrected WOB

---

### Why Windowing Works

It approximates synchronization:

$$
\text{surface fast signal} \Rightarrow \text{quasi-steady average}
$$

so both sensors represent the same physical state.

---

## Final Result

The algorithm produces a real-time estimate:

True WOB independent of
- flowrate
- block position
- sensor location

by combining physics-based decomposition with data-driven calibration.
