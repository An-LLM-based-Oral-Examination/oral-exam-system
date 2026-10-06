import { useEffect, useRef } from 'react'
import * as THREE from 'three'

interface FloatingNode {
  mesh: THREE.Mesh
  baseScale: number
  phase: number
}

interface PulseRing {
  mesh: THREE.Mesh
  material: THREE.MeshBasicMaterial
  phase: number
}

function createSeededRandom(seed = 8_731) {
  let currentSeed = seed

  return () => {
    currentSeed = (currentSeed * 16_807) % 2_147_483_647
    return (currentSeed - 1) / 2_147_483_646
  }
}

function createRoundedPanelGeometry(width: number, height: number, radius: number, depth: number) {
  const left = -width / 2
  const bottom = -height / 2
  const right = width / 2
  const top = height / 2
  const shape = new THREE.Shape()

  shape.moveTo(left + radius, bottom)
  shape.lineTo(right - radius, bottom)
  shape.quadraticCurveTo(right, bottom, right, bottom + radius)
  shape.lineTo(right, top - radius)
  shape.quadraticCurveTo(right, top, right - radius, top)
  shape.lineTo(left + radius, top)
  shape.quadraticCurveTo(left, top, left, top - radius)
  shape.lineTo(left, bottom + radius)
  shape.quadraticCurveTo(left, bottom, left + radius, bottom)

  return new THREE.ExtrudeGeometry(shape, {
    depth,
    bevelEnabled: true,
    bevelSegments: 4,
    bevelSize: 0.025,
    bevelThickness: 0.025,
    curveSegments: 16,
    steps: 1,
  })
}

function createOrbit(
  radiusX: number,
  radiusY: number,
  color: number,
  opacity: number,
  dashed = false,
) {
  const points: THREE.Vector3[] = []

  for (let index = 0; index < 128; index += 1) {
    const angle = (index / 128) * Math.PI * 2
    points.push(new THREE.Vector3(Math.cos(angle) * radiusX, Math.sin(angle) * radiusY, 0))
  }

  const geometry = new THREE.BufferGeometry().setFromPoints(points)
  const material = dashed
    ? new THREE.LineDashedMaterial({
        color,
        dashSize: 0.13,
        gapSize: 0.09,
        opacity,
        transparent: true,
        depthWrite: false,
      })
    : new THREE.LineBasicMaterial({
        color,
        opacity,
        transparent: true,
        depthWrite: false,
      })
  const orbit = new THREE.LineLoop(geometry, material)

  if (dashed) orbit.computeLineDistances()
  return orbit
}

export function StudentOverviewThreeScene() {
  const mountRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const mount = mountRef.current
    if (!mount) return

    const random = createSeededRandom()
    const scene = new THREE.Scene()
    const camera = new THREE.PerspectiveCamera(33, 1, 0.1, 100)
    camera.position.set(0, 0.05, 9.8)

    let renderer: THREE.WebGLRenderer

    try {
      renderer = new THREE.WebGLRenderer({
        alpha: true,
        antialias: true,
        powerPreference: 'high-performance',
      })
    } catch {
      return
    }

    renderer.setClearColor(0x000000, 0)
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 1.6))
    renderer.outputColorSpace = THREE.SRGBColorSpace
    renderer.toneMapping = THREE.ACESFilmicToneMapping
    renderer.toneMappingExposure = 0.96
    renderer.domElement.setAttribute('aria-hidden', 'true')
    renderer.domElement.style.display = 'block'
    renderer.domElement.style.width = '100%'
    renderer.domElement.style.height = '100%'
    mount.appendChild(renderer.domElement)

    const hemisphereLight = new THREE.HemisphereLight(0xf2fdff, 0x102b62, 1.25)
    const keyLight = new THREE.DirectionalLight(0xd9fbff, 2.6)
    const fillLight = new THREE.DirectionalLight(0x4f75ff, 1.45)
    const cyanLight = new THREE.PointLight(0x4deaff, 9, 10, 1.7)
    const blueRimLight = new THREE.PointLight(0x315deb, 6, 9, 1.8)
    keyLight.position.set(4.5, 6, 7)
    fillLight.position.set(-4, 1, 2)
    cyanLight.position.set(2.5, 2.1, 3.4)
    blueRimLight.position.set(-2.8, -1.2, 2)
    scene.add(hemisphereLight, keyLight, fillLight, cyanLight, blueRimLight)

    const rootGroup = new THREE.Group()
    rootGroup.position.set(0.08, 0.08, 0)
    rootGroup.scale.setScalar(0.9)
    scene.add(rootGroup)

    const haloGroup = new THREE.Group()
    haloGroup.position.z = -1.45
    rootGroup.add(haloGroup)

    const haloDisc = new THREE.Mesh(
      new THREE.CircleGeometry(2.35, 72),
      new THREE.MeshBasicMaterial({
        color: 0x78e9ff,
        opacity: 0.055,
        transparent: true,
        depthWrite: false,
      }),
    )
    const haloRing = new THREE.Mesh(
      new THREE.RingGeometry(1.65, 1.69, 72),
      new THREE.MeshBasicMaterial({
        color: 0x5adff5,
        opacity: 0.13,
        transparent: true,
        side: THREE.DoubleSide,
        depthWrite: false,
      }),
    )
    haloGroup.add(haloDisc, haloRing)

    const orbitSystem = new THREE.Group()
    orbitSystem.position.set(0, 0.08, -0.55)
    rootGroup.add(orbitSystem)

    const orbitOneGroup = new THREE.Group()
    orbitOneGroup.rotation.set(0.54, 0.25, -0.3)
    const orbitOne = createOrbit(2.75, 1.48, 0x38cee9, 0.34)
    orbitOneGroup.add(orbitOne)

    const orbitTwoGroup = new THREE.Group()
    orbitTwoGroup.rotation.set(-0.48, -0.3, 0.88)
    const orbitTwo = createOrbit(2.62, 1.42, 0x6a78e8, 0.27)
    orbitTwoGroup.add(orbitTwo)

    const orbitThreeGroup = new THREE.Group()
    orbitThreeGroup.rotation.set(0.85, 0.16, 0.25)
    const orbitThree = createOrbit(2.18, 1.1, 0x56bfdc, 0.16, true)
    orbitThreeGroup.add(orbitThree)
    orbitSystem.add(orbitOneGroup, orbitTwoGroup, orbitThreeGroup)

    const cageMaterial = new THREE.MeshBasicMaterial({
      color: 0x83eaf3,
      transparent: true,
      opacity: 0.17,
      wireframe: true,
      depthWrite: false,
    })
    const grilleRibMaterial = new THREE.MeshBasicMaterial({
      color: 0x6ce1ed,
      opacity: 0.42,
      transparent: true,
      depthWrite: false,
    })
    const headMaterial = new THREE.MeshPhysicalMaterial({
      color: 0x0a5278,
      emissive: 0x061f3f,
      emissiveIntensity: 0.18,
      metalness: 0.58,
      roughness: 0.3,
      clearcoat: 0.78,
      clearcoatRoughness: 0.18,
    })
    const bodyMaterial = new THREE.MeshPhysicalMaterial({
      color: 0x174ec2,
      emissive: 0x061e66,
      emissiveIntensity: 0.2,
      metalness: 0.62,
      roughness: 0.25,
      clearcoat: 0.88,
      clearcoatRoughness: 0.16,
    })
    const navyMaterial = new THREE.MeshStandardMaterial({
      color: 0x06193e,
      emissive: 0x071946,
      emissiveIntensity: 0.32,
      metalness: 0.42,
      roughness: 0.3,
    })
    const silverMaterial = new THREE.MeshPhysicalMaterial({
      color: 0xb9eaf4,
      emissive: 0x26697e,
      emissiveIntensity: 0.18,
      metalness: 0.78,
      roughness: 0.2,
      clearcoat: 0.78,
    })
    const cyanTrimMaterial = new THREE.MeshBasicMaterial({
      color: 0x5de9f6,
      opacity: 0.82,
      transparent: true,
    })

    const microphoneGroup = new THREE.Group()
    microphoneGroup.position.z = 0.28
    rootGroup.add(microphoneGroup)

    const headCylinder = new THREE.Mesh(new THREE.CylinderGeometry(0.62, 0.62, 1, 40), headMaterial)
    headCylinder.position.y = 0.42

    const headDome = new THREE.Mesh(
      new THREE.SphereGeometry(0.62, 40, 20, 0, Math.PI * 2, 0, Math.PI / 2),
      headMaterial,
    )
    headDome.position.y = 0.92

    const cageCylinder = new THREE.Mesh(
      new THREE.CylinderGeometry(0.65, 0.65, 1.02, 18, 5, true),
      cageMaterial,
    )
    cageCylinder.position.y = 0.42
    cageCylinder.renderOrder = 3

    const cageDome = new THREE.Mesh(
      new THREE.SphereGeometry(0.65, 18, 9, 0, Math.PI * 2, 0, Math.PI / 2),
      cageMaterial,
    )
    cageDome.position.y = 0.92
    cageDome.renderOrder = 3

    const grilleRibs: THREE.Mesh[] = []
    for (let index = 0; index < 5; index += 1) {
      const rib = new THREE.Mesh(new THREE.TorusGeometry(0.635, 0.012, 10, 64), grilleRibMaterial)
      rib.rotation.x = Math.PI / 2
      rib.position.y = 0.05 + index * 0.21
      rib.renderOrder = 3
      grilleRibs.push(rib)
    }

    const lowerBody = new THREE.Mesh(new THREE.CylinderGeometry(0.55, 0.46, 1.28, 36), bodyMaterial)
    lowerBody.position.y = -0.75

    const bodyCap = new THREE.Mesh(new THREE.SphereGeometry(0.46, 30, 18), bodyMaterial)
    bodyCap.scale.y = 0.28
    bodyCap.position.y = -1.39

    const headCollar = new THREE.Mesh(new THREE.TorusGeometry(0.64, 0.055, 16, 72), silverMaterial)
    headCollar.rotation.x = Math.PI / 2
    headCollar.position.y = -0.1

    const cyanCollar = new THREE.Mesh(
      new THREE.TorusGeometry(0.66, 0.018, 12, 72),
      cyanTrimMaterial,
    )
    cyanCollar.rotation.x = Math.PI / 2
    cyanCollar.position.y = -0.075

    const grillePlate = new THREE.Mesh(
      createRoundedPanelGeometry(0.4, 0.68, 0.12, 0.045),
      navyMaterial,
    )
    grillePlate.position.set(0, 0.43, 0.594)
    grillePlate.renderOrder = 4

    const grilleLineMaterial = new THREE.MeshStandardMaterial({
      color: 0x66deee,
      emissive: 0x2ab7d4,
      emissiveIntensity: 0.7,
      metalness: 0.34,
      roughness: 0.28,
    })
    const grilleLines: THREE.Mesh[] = []
    for (let index = 0; index < 6; index += 1) {
      const grilleLine = new THREE.Mesh(
        new THREE.BoxGeometry(0.22 - Math.abs(index - 2.5) * 0.012, 0.015, 0.018),
        grilleLineMaterial,
      )
      grilleLine.position.set(0, 0.2 + index * 0.09, 0.69)
      grilleLine.renderOrder = 5
      grilleLines.push(grilleLine)
    }

    const statusLightMaterial = new THREE.MeshStandardMaterial({
      color: 0xc6ffff,
      emissive: 0x45e9f5,
      emissiveIntensity: 1.15,
      roughness: 0.12,
    })
    const statusLight = new THREE.Mesh(new THREE.SphereGeometry(0.038, 16, 16), statusLightMaterial)
    statusLight.position.set(0, 0.79, 0.695)
    statusLight.renderOrder = 5

    const bodyAccentMaterial = new THREE.MeshBasicMaterial({
      color: 0x5bd9f2,
      opacity: 0.2,
      transparent: true,
    })
    const bodyAccents: THREE.Mesh[] = []
    for (let index = 0; index < 3; index += 1) {
      const accent = new THREE.Mesh(
        new THREE.TorusGeometry(0.51 - index * 0.018, 0.012, 10, 56),
        bodyAccentMaterial,
      )
      accent.rotation.x = Math.PI / 2
      accent.position.y = -0.61 - index * 0.28
      bodyAccents.push(accent)
    }

    microphoneGroup.add(
      headCylinder,
      headDome,
      cageCylinder,
      cageDome,
      ...grilleRibs,
      lowerBody,
      bodyCap,
      headCollar,
      cyanCollar,
      grillePlate,
      ...grilleLines,
      statusLight,
      ...bodyAccents,
    )

    const yokeGroup = new THREE.Group()
    yokeGroup.position.z = 0.02
    microphoneGroup.add(yokeGroup)

    const yokeMaterial = new THREE.MeshPhysicalMaterial({
      color: 0x6f9fbd,
      emissive: 0x0b2b48,
      emissiveIntensity: 0.12,
      metalness: 0.84,
      roughness: 0.2,
      clearcoat: 0.72,
    })
    const leftArm = new THREE.Mesh(new THREE.CylinderGeometry(0.052, 0.052, 0.38, 16), yokeMaterial)
    const rightArm = leftArm.clone()
    leftArm.position.set(-0.79, -0.46, 0)
    rightArm.position.set(0.79, -0.46, 0)

    const yokeArc = new THREE.Mesh(
      new THREE.TorusGeometry(0.79, 0.052, 14, 72, Math.PI),
      yokeMaterial,
    )
    yokeArc.position.y = -0.58
    yokeArc.rotation.z = Math.PI

    const pivotGeometry = new THREE.CylinderGeometry(0.135, 0.135, 0.11, 28)
    const leftPivot = new THREE.Mesh(pivotGeometry, silverMaterial)
    const rightPivot = new THREE.Mesh(pivotGeometry, silverMaterial)
    leftPivot.rotation.z = Math.PI / 2
    rightPivot.rotation.z = Math.PI / 2
    leftPivot.position.set(-0.74, -0.31, 0)
    rightPivot.position.set(0.74, -0.31, 0)

    const leftPivotInset = new THREE.Mesh(
      new THREE.CylinderGeometry(0.07, 0.07, 0.125, 24),
      cyanTrimMaterial,
    )
    const rightPivotInset = leftPivotInset.clone()
    leftPivotInset.rotation.z = Math.PI / 2
    rightPivotInset.rotation.z = Math.PI / 2
    leftPivotInset.position.copy(leftPivot.position)
    rightPivotInset.position.copy(rightPivot.position)
    yokeGroup.add(
      leftArm,
      rightArm,
      yokeArc,
      leftPivot,
      rightPivot,
      leftPivotInset,
      rightPivotInset,
    )

    const standRod = new THREE.Mesh(
      new THREE.CylinderGeometry(0.08, 0.095, 0.72, 20),
      silverMaterial,
    )
    standRod.position.y = -1.84

    const standConnector = new THREE.Mesh(
      new THREE.CylinderGeometry(0.2, 0.24, 0.17, 28),
      yokeMaterial,
    )
    standConnector.position.y = -1.51

    const standBase = new THREE.Mesh(new THREE.CylinderGeometry(0.98, 1.13, 0.19, 48), navyMaterial)
    standBase.position.y = -2.26

    const baseTop = new THREE.Mesh(new THREE.CylinderGeometry(0.9, 0.98, 0.075, 48), bodyMaterial)
    baseTop.position.y = -2.16

    const baseRing = new THREE.Mesh(new THREE.TorusGeometry(0.98, 0.018, 12, 72), cyanTrimMaterial)
    baseRing.rotation.x = Math.PI / 2
    baseRing.position.y = -2.12

    const floorGlow = new THREE.Mesh(
      new THREE.RingGeometry(0.82, 1.42, 72),
      new THREE.MeshBasicMaterial({
        color: 0x4bdff2,
        opacity: 0.13,
        transparent: true,
        side: THREE.DoubleSide,
        blending: THREE.AdditiveBlending,
        depthWrite: false,
      }),
    )
    floorGlow.rotation.x = -Math.PI / 2
    floorGlow.position.y = -2.38
    microphoneGroup.add(standConnector, standRod, standBase, baseTop, baseRing, floorGlow)

    const nodeMaterial = new THREE.MeshPhysicalMaterial({
      color: 0xd4fdff,
      emissive: 0x2ed8e6,
      emissiveIntensity: 0.95,
      metalness: 0.16,
      roughness: 0.15,
      clearcoat: 1,
    })
    const floatingNodes: FloatingNode[] = []

    const addOrbitNodes = (
      parent: THREE.Group,
      radiusX: number,
      radiusY: number,
      count: number,
    ) => {
      for (let index = 0; index < count; index += 1) {
        const angle = (index / count) * Math.PI * 2 + random() * 0.3
        const baseScale = index % 3 === 0 ? 1.3 : 0.82
        const node = new THREE.Mesh(new THREE.SphereGeometry(0.075, 16, 16), nodeMaterial)
        node.position.set(Math.cos(angle) * radiusX, Math.sin(angle) * radiusY, 0)
        node.scale.setScalar(baseScale)
        parent.add(node)
        floatingNodes.push({ mesh: node, baseScale, phase: random() * Math.PI * 2 })
      }
    }

    addOrbitNodes(orbitOneGroup, 2.75, 1.48, 5)
    addOrbitNodes(orbitTwoGroup, 2.62, 1.42, 4)

    const particleCount = 42
    const particlePositions = new Float32Array(particleCount * 3)
    for (let index = 0; index < particleCount; index += 1) {
      particlePositions[index * 3] = (random() - 0.5) * 7.5
      particlePositions[index * 3 + 1] = (random() - 0.5) * 5.2
      particlePositions[index * 3 + 2] = (random() - 0.5) * 2.5 - 1
    }
    const particleGeometry = new THREE.BufferGeometry()
    particleGeometry.setAttribute('position', new THREE.BufferAttribute(particlePositions, 3))
    const particleMaterial = new THREE.PointsMaterial({
      color: 0x48c6e9,
      size: 0.045,
      opacity: 0.4,
      transparent: true,
      blending: THREE.AdditiveBlending,
      depthWrite: false,
    })
    const particles = new THREE.Points(particleGeometry, particleMaterial)
    scene.add(particles)

    const pulseRings: PulseRing[] = []
    for (let index = 0; index < 2; index += 1) {
      const material = new THREE.MeshBasicMaterial({
        color: index === 0 ? 0x61e7f4 : 0x6677e6,
        opacity: 0.12,
        transparent: true,
        side: THREE.DoubleSide,
        depthWrite: false,
      })
      const ring = new THREE.Mesh(new THREE.RingGeometry(1.02, 1.045, 64), material)
      ring.position.set(0, 0.76, -0.92 - index * 0.1)
      rootGroup.add(ring)
      pulseRings.push({ mesh: ring, material, phase: index * Math.PI })
    }

    const targetPointer = new THREE.Vector2()
    const smoothPointer = new THREE.Vector2()
    const motionPreference = window.matchMedia('(prefers-reduced-motion: reduce)')
    let reduceMotion = motionPreference.matches
    let isInViewport = true
    let isDocumentVisible = document.visibilityState !== 'hidden'
    const startedAt = performance.now()
    const orbitOneBaseRotation = orbitOneGroup.rotation.clone()
    const orbitTwoBaseRotation = orbitTwoGroup.rotation.clone()
    const orbitThreeBaseRotation = orbitThreeGroup.rotation.clone()

    const resetPose = () => {
      targetPointer.set(0, 0)
      smoothPointer.set(0, 0)
      rootGroup.position.set(0.08, 0.08, 0)
      rootGroup.rotation.set(0, 0, 0)
      microphoneGroup.position.set(0, 0, 0.28)
      microphoneGroup.rotation.set(0, 0, 0)
      orbitOneGroup.rotation.copy(orbitOneBaseRotation)
      orbitTwoGroup.rotation.copy(orbitTwoBaseRotation)
      orbitThreeGroup.rotation.copy(orbitThreeBaseRotation)
      particles.position.set(0, 0, 0)
    }

    const renderFrame = (time = performance.now()) => {
      const elapsed = (time - startedAt) * 0.001

      if (!reduceMotion && isDocumentVisible && isInViewport) {
        smoothPointer.lerp(targetPointer, 0.055)
        rootGroup.position.x = 0.08 + smoothPointer.x * 0.17
        rootGroup.position.y = 0.08 - smoothPointer.y * 0.12
        rootGroup.rotation.y = smoothPointer.x * 0.15
        rootGroup.rotation.x = -smoothPointer.y * 0.085

        microphoneGroup.position.y = Math.sin(elapsed * 0.9) * 0.045
        microphoneGroup.rotation.y = Math.sin(elapsed * 0.42) * 0.055
        orbitOneGroup.rotation.z = orbitOneBaseRotation.z + elapsed * 0.09
        orbitTwoGroup.rotation.z = orbitTwoBaseRotation.z - elapsed * 0.072
        orbitThreeGroup.rotation.z = orbitThreeBaseRotation.z + elapsed * 0.052
        haloRing.rotation.z = elapsed * 0.035
        particles.rotation.y = elapsed * 0.02
        particles.position.x = smoothPointer.x * -0.09
        particles.position.y = smoothPointer.y * 0.06

        statusLightMaterial.emissiveIntensity = 0.9 + Math.sin(elapsed * 2.2) * 0.25
        cyanTrimMaterial.opacity = 0.74 + Math.sin(elapsed * 1.35) * 0.08

        floatingNodes.forEach(({ mesh, baseScale, phase }) => {
          const scale = baseScale * (1 + Math.sin(elapsed * 1.3 + phase) * 0.12)
          mesh.scale.setScalar(scale)
        })

        pulseRings.forEach(({ mesh, material, phase }) => {
          const progress = (Math.sin(elapsed * 1.12 + phase) + 1) / 2
          mesh.scale.setScalar(0.92 + progress * 0.3)
          material.opacity = 0.14 - progress * 0.075
        })
      }

      renderer.render(scene, camera)
    }

    const syncAnimationLoop = () => {
      const shouldAnimate = !reduceMotion && isDocumentVisible && isInViewport
      renderer.setAnimationLoop(shouldAnimate ? renderFrame : null)

      if (!shouldAnimate) {
        resetPose()
        renderFrame()
      }
    }

    const handlePointerMove = (event: PointerEvent) => {
      if (reduceMotion) return

      const rect = mount.getBoundingClientRect()
      if (rect.width === 0 || rect.height === 0) return

      const relativeX = (event.clientX - rect.left) / rect.width
      const relativeY = (event.clientY - rect.top) / rect.height

      if (relativeX < -0.45 || relativeX > 1.2 || relativeY < -0.2 || relativeY > 1.2) {
        targetPointer.set(0, 0)
        return
      }

      targetPointer.set((relativeX - 0.5) * 2, (relativeY - 0.5) * 2)
    }

    const resizeScene = () => {
      const { width, height } = mount.getBoundingClientRect()
      if (width === 0 || height === 0) return

      renderer.setPixelRatio(Math.min(window.devicePixelRatio, 1.6))
      renderer.setSize(width, height, false)
      camera.aspect = width / height
      camera.updateProjectionMatrix()
      renderFrame()
    }

    const handleVisibilityChange = () => {
      isDocumentVisible = document.visibilityState !== 'hidden'
      syncAnimationLoop()
    }

    const handleMotionPreference = (event: MediaQueryListEvent) => {
      reduceMotion = event.matches
      syncAnimationLoop()
    }

    const resizeObserver = new ResizeObserver(resizeScene)
    resizeObserver.observe(mount)

    const intersectionObserver = new IntersectionObserver(
      ([entry]) => {
        isInViewport = entry.isIntersecting
        syncAnimationLoop()
      },
      { threshold: 0.05 },
    )
    intersectionObserver.observe(mount)

    window.addEventListener('pointermove', handlePointerMove, { passive: true })
    document.addEventListener('visibilitychange', handleVisibilityChange)
    motionPreference.addEventListener('change', handleMotionPreference)

    resizeScene()
    syncAnimationLoop()

    return () => {
      renderer.setAnimationLoop(null)
      resizeObserver.disconnect()
      intersectionObserver.disconnect()
      window.removeEventListener('pointermove', handlePointerMove)
      document.removeEventListener('visibilitychange', handleVisibilityChange)
      motionPreference.removeEventListener('change', handleMotionPreference)

      const geometries = new Set<THREE.BufferGeometry>()
      const materials = new Set<THREE.Material>()
      scene.traverse((object) => {
        const renderable = object as THREE.Object3D & {
          geometry?: THREE.BufferGeometry
          material?: THREE.Material | THREE.Material[]
        }

        if (renderable.geometry) geometries.add(renderable.geometry)
        if (Array.isArray(renderable.material)) {
          renderable.material.forEach((material) => materials.add(material))
        } else if (renderable.material) {
          materials.add(renderable.material)
        }
      })

      geometries.forEach((geometry) => geometry.dispose())
      materials.forEach((material) => material.dispose())
      renderer.dispose()
      renderer.forceContextLoss()

      if (renderer.domElement.parentElement === mount) {
        mount.removeChild(renderer.domElement)
      }
    }
  }, [])

  return <div ref={mountRef} className="pointer-events-none absolute inset-0" aria-hidden="true" />
}
