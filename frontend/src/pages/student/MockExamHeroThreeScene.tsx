import { useEffect, useRef } from 'react'
import * as THREE from 'three'

interface MockExamHeroThreeSceneProps {
  scanning?: boolean
  ready?: boolean
}

interface PulseNode {
  mesh: THREE.Mesh
  phase: number
  scale: number
}

function createSeededRandom(seed = 19_987) {
  let currentSeed = seed

  return () => {
    currentSeed = (currentSeed * 16_807) % 2_147_483_647
    return (currentSeed - 1) / 2_147_483_646
  }
}

function createEllipse(radiusX: number, radiusY: number, color: number, opacity: number) {
  const points: THREE.Vector3[] = []

  for (let index = 0; index < 128; index += 1) {
    const angle = (index / 128) * Math.PI * 2
    points.push(new THREE.Vector3(Math.cos(angle) * radiusX, Math.sin(angle) * radiusY, 0))
  }

  return new THREE.LineLoop(
    new THREE.BufferGeometry().setFromPoints(points),
    new THREE.LineBasicMaterial({ color, opacity, transparent: true, depthWrite: false }),
  )
}

export function MockExamHeroThreeScene({
  scanning = false,
  ready = false,
}: MockExamHeroThreeSceneProps) {
  const mountRef = useRef<HTMLDivElement>(null)
  const scanningRef = useRef(scanning)
  const readyRef = useRef(ready)

  useEffect(() => {
    scanningRef.current = scanning
  }, [scanning])

  useEffect(() => {
    readyRef.current = ready
  }, [ready])

  useEffect(() => {
    const mount = mountRef.current
    if (!mount) return

    const random = createSeededRandom()
    const scene = new THREE.Scene()
    const camera = new THREE.PerspectiveCamera(32, 1, 0.1, 50)
    camera.position.set(0, 0.05, 7.8)

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
    renderer.toneMappingExposure = 1.02
    renderer.domElement.setAttribute('aria-hidden', 'true')
    renderer.domElement.style.display = 'block'
    renderer.domElement.style.width = '100%'
    renderer.domElement.style.height = '100%'
    mount.appendChild(renderer.domElement)

    const hemisphereLight = new THREE.HemisphereLight(0xf0fcff, 0x06162f, 1.45)
    const keyLight = new THREE.DirectionalLight(0xe2faff, 3.1)
    const cyanLight = new THREE.PointLight(0x4ae7f4, 11, 10, 1.7)
    const blueLight = new THREE.PointLight(0x315eea, 9, 9, 1.8)
    const amberLight = new THREE.PointLight(0xffc863, 5, 7, 1.9)
    keyLight.position.set(4, 5, 5)
    cyanLight.position.set(2.2, 1.5, 3.2)
    blueLight.position.set(-2.4, -0.8, 2.6)
    amberLight.position.set(0.2, 2.2, 2.2)
    scene.add(hemisphereLight, keyLight, cyanLight, blueLight, amberLight)

    const rootGroup = new THREE.Group()
    rootGroup.position.set(0.38, -0.02, 0)
    rootGroup.scale.setScalar(0.94)
    scene.add(rootGroup)

    const orbitBack = new THREE.Group()
    orbitBack.position.z = -0.8
    rootGroup.add(orbitBack)

    const orbitOneGroup = new THREE.Group()
    orbitOneGroup.rotation.set(0.62, 0.2, -0.28)
    orbitOneGroup.add(createEllipse(1.72, 0.72, 0x62e7f3, 0.42))
    orbitBack.add(orbitOneGroup)

    const orbitTwoGroup = new THREE.Group()
    orbitTwoGroup.rotation.set(-0.48, -0.25, 0.86)
    orbitTwoGroup.add(createEllipse(1.55, 0.66, 0x718bff, 0.32))
    orbitBack.add(orbitTwoGroup)

    const dialGroup = new THREE.Group()
    dialGroup.position.z = 0.12
    rootGroup.add(dialGroup)

    const navyMaterial = new THREE.MeshPhysicalMaterial({
      color: 0x0a2451,
      emissive: 0x071c48,
      emissiveIntensity: 0.26,
      metalness: 0.72,
      roughness: 0.24,
      clearcoat: 0.86,
      clearcoatRoughness: 0.14,
    })
    const blueMaterial = new THREE.MeshPhysicalMaterial({
      color: 0x225bd5,
      emissive: 0x123b99,
      emissiveIntensity: 0.44,
      metalness: 0.55,
      roughness: 0.21,
      clearcoat: 0.9,
      clearcoatRoughness: 0.12,
    })
    const cyanMaterial = new THREE.MeshStandardMaterial({
      color: 0x76eff8,
      emissive: 0x2bbdce,
      emissiveIntensity: 0.72,
      metalness: 0.38,
      roughness: 0.2,
    })
    const amberMaterial = new THREE.MeshStandardMaterial({
      color: 0xffd27a,
      emissive: 0xd98d21,
      emissiveIntensity: 0.58,
      metalness: 0.46,
      roughness: 0.23,
    })

    const dialBack = new THREE.Mesh(new THREE.CylinderGeometry(0.86, 0.86, 0.16, 64), navyMaterial)
    dialBack.rotation.x = Math.PI / 2
    dialGroup.add(dialBack)

    const dialFace = new THREE.Mesh(
      new THREE.CircleGeometry(0.74, 64),
      new THREE.MeshPhysicalMaterial({
        color: 0x102f67,
        emissive: 0x09275c,
        emissiveIntensity: 0.35,
        metalness: 0.35,
        roughness: 0.28,
        clearcoat: 0.75,
      }),
    )
    dialFace.position.z = 0.1
    dialGroup.add(dialFace)

    const outerRim = new THREE.Mesh(new THREE.TorusGeometry(0.86, 0.055, 14, 80), blueMaterial)
    outerRim.position.z = 0.12
    dialGroup.add(outerRim)

    const innerRim = new THREE.Mesh(new THREE.TorusGeometry(0.67, 0.014, 10, 72), cyanMaterial)
    innerRim.position.z = 0.13
    dialGroup.add(innerRim)

    const tickGroup = new THREE.Group()
    tickGroup.position.z = 0.145
    dialGroup.add(tickGroup)
    for (let index = 0; index < 24; index += 1) {
      const angle = (index / 24) * Math.PI * 2
      const isMajor = index % 6 === 0
      const tick = new THREE.Mesh(
        new THREE.BoxGeometry(isMajor ? 0.04 : 0.025, isMajor ? 0.14 : 0.085, 0.018),
        isMajor ? amberMaterial : cyanMaterial,
      )
      tick.position.set(Math.sin(angle) * 0.57, Math.cos(angle) * 0.57, 0)
      tick.rotation.z = -angle
      tickGroup.add(tick)
    }

    const minuteHand = new THREE.Mesh(new THREE.BoxGeometry(0.055, 0.49, 0.045), cyanMaterial)
    minuteHand.position.set(0, 0.2, 0.19)
    dialGroup.add(minuteHand)

    const hourHandPivot = new THREE.Group()
    hourHandPivot.position.z = 0.19
    hourHandPivot.rotation.z = -0.9
    dialGroup.add(hourHandPivot)
    const hourHand = new THREE.Mesh(new THREE.BoxGeometry(0.07, 0.34, 0.052), amberMaterial)
    hourHand.position.y = 0.13
    hourHandPivot.add(hourHand)

    const centerHub = new THREE.Mesh(new THREE.SphereGeometry(0.105, 24, 24), amberMaterial)
    centerHub.position.z = 0.24
    dialGroup.add(centerHub)

    const crownStem = new THREE.Mesh(new THREE.BoxGeometry(0.16, 0.22, 0.17), navyMaterial)
    crownStem.position.set(0, 0.97, -0.01)
    dialGroup.add(crownStem)
    const crown = new THREE.Mesh(new THREE.CylinderGeometry(0.23, 0.23, 0.12, 28), blueMaterial)
    crown.rotation.x = Math.PI / 2
    crown.position.set(0, 1.09, 0)
    dialGroup.add(crown)

    const scanMaterial = new THREE.MeshBasicMaterial({
      color: 0x74f3fb,
      opacity: 0.2,
      transparent: true,
      side: THREE.DoubleSide,
      depthWrite: false,
    })
    const scanRing = new THREE.Mesh(new THREE.RingGeometry(1.03, 1.06, 72), scanMaterial)
    scanRing.position.z = -0.28
    rootGroup.add(scanRing)

    const shieldCore = new THREE.Mesh(new THREE.OctahedronGeometry(0.34, 1), blueMaterial)
    shieldCore.position.set(1.25, -0.74, -0.2)
    shieldCore.scale.set(0.72, 1, 0.45)
    rootGroup.add(shieldCore)
    const shieldCheck = new THREE.Mesh(new THREE.TorusGeometry(0.16, 0.025, 10, 40), cyanMaterial)
    shieldCheck.position.set(1.25, -0.74, 0.04)
    shieldCheck.scale.y = 0.65
    rootGroup.add(shieldCheck)

    const nodeMaterial = new THREE.MeshStandardMaterial({
      color: 0xc9fbff,
      emissive: 0x4cd5e6,
      emissiveIntensity: 0.78,
      metalness: 0.2,
      roughness: 0.18,
    })
    const pulseNodes: PulseNode[] = []
    const addNodes = (parent: THREE.Group, radiusX: number, radiusY: number, count: number) => {
      for (let index = 0; index < count; index += 1) {
        const angle = (index / count) * Math.PI * 2 + 0.2
        const scale = index % 2 === 0 ? 1 : 0.72
        const node = new THREE.Mesh(new THREE.SphereGeometry(0.055, 14, 14), nodeMaterial)
        node.position.set(Math.cos(angle) * radiusX, Math.sin(angle) * radiusY, 0)
        node.scale.setScalar(scale)
        parent.add(node)
        pulseNodes.push({ mesh: node, phase: index * 0.85, scale })
      }
    }
    addNodes(orbitOneGroup, 1.72, 0.72, 5)
    addNodes(orbitTwoGroup, 1.55, 0.66, 4)

    const particleCount = 34
    const particlePositions = new Float32Array(particleCount * 3)
    for (let index = 0; index < particleCount; index += 1) {
      particlePositions[index * 3] = (random() - 0.5) * 5
      particlePositions[index * 3 + 1] = (random() - 0.5) * 3.2
      particlePositions[index * 3 + 2] = (random() - 0.5) * 2 - 1
    }
    const particleGeometry = new THREE.BufferGeometry()
    particleGeometry.setAttribute('position', new THREE.BufferAttribute(particlePositions, 3))
    const particles = new THREE.Points(
      particleGeometry,
      new THREE.PointsMaterial({
        color: 0x65e9f5,
        size: 0.038,
        opacity: 0.44,
        transparent: true,
        blending: THREE.AdditiveBlending,
        depthWrite: false,
      }),
    )
    rootGroup.add(particles)

    const pointerTarget = new THREE.Vector2()
    const pointerCurrent = new THREE.Vector2()
    const motionPreference = window.matchMedia('(prefers-reduced-motion: reduce)')
    let reduceMotion = motionPreference.matches
    let isDocumentVisible = document.visibilityState !== 'hidden'
    let isInViewport = true
    let mountRect = mount.getBoundingClientRect()
    const startedAt = performance.now()
    const orbitOneRotation = orbitOneGroup.rotation.clone()
    const orbitTwoRotation = orbitTwoGroup.rotation.clone()

    const resetPose = () => {
      pointerTarget.set(0, 0)
      pointerCurrent.set(0, 0)
      rootGroup.position.set(0.38, -0.02, 0)
      rootGroup.rotation.set(0, 0, 0)
      dialGroup.position.set(0, 0, 0.12)
      dialGroup.rotation.set(0, 0, 0)
      orbitOneGroup.rotation.copy(orbitOneRotation)
      orbitTwoGroup.rotation.copy(orbitTwoRotation)
      scanRing.scale.setScalar(1)
    }

    const renderFrame = (time = performance.now()) => {
      const elapsed = (time - startedAt) * 0.001

      if (!reduceMotion && isDocumentVisible && isInViewport) {
        const speed = scanningRef.current ? 2.2 : 1
        pointerCurrent.lerp(pointerTarget, 0.055)
        rootGroup.position.x = 0.38 + pointerCurrent.x * 0.13
        rootGroup.position.y = -0.02 - pointerCurrent.y * 0.085
        rootGroup.rotation.y = pointerCurrent.x * 0.14
        rootGroup.rotation.x = -pointerCurrent.y * 0.08

        dialGroup.position.y = Math.sin(elapsed * 0.95) * 0.045
        dialGroup.rotation.y = Math.sin(elapsed * 0.42) * 0.055
        minuteHand.rotation.z = -elapsed * 0.26 * speed
        hourHandPivot.rotation.z = -0.9 - elapsed * 0.045 * speed
        outerRim.rotation.z = elapsed * 0.08
        orbitOneGroup.rotation.z = orbitOneRotation.z + elapsed * 0.1 * speed
        orbitTwoGroup.rotation.z = orbitTwoRotation.z - elapsed * 0.078 * speed
        particles.rotation.y = elapsed * 0.025

        const pulseProgress = (Math.sin(elapsed * 1.6 * speed) + 1) / 2
        scanRing.scale.setScalar(0.92 + pulseProgress * 0.3)
        scanMaterial.opacity = 0.23 - pulseProgress * 0.13
        cyanMaterial.emissiveIntensity =
          (readyRef.current ? 1.02 : 0.68) + Math.sin(elapsed * 2.2) * 0.08

        pulseNodes.forEach(({ mesh, phase, scale }) => {
          mesh.scale.setScalar(scale * (1 + Math.sin(elapsed * 1.55 + phase) * 0.14))
        })
      }

      renderer.render(scene, camera)
    }

    const syncAnimation = () => {
      const shouldAnimate = !reduceMotion && isDocumentVisible && isInViewport
      renderer.setAnimationLoop(shouldAnimate ? renderFrame : null)
      if (!shouldAnimate) {
        resetPose()
        renderFrame()
      }
    }

    const handlePointerMove = (event: PointerEvent) => {
      if (reduceMotion || !isInViewport) return
      if (mountRect.width === 0 || mountRect.height === 0) return
      const relativeX = (event.clientX - mountRect.left) / mountRect.width
      const relativeY = (event.clientY - mountRect.top) / mountRect.height

      if (relativeX < -0.3 || relativeX > 1.3 || relativeY < -0.5 || relativeY > 1.5) {
        pointerTarget.set(0, 0)
        return
      }
      pointerTarget.set((relativeX - 0.5) * 2, (relativeY - 0.5) * 2)
    }

    const resizeScene = () => {
      mountRect = mount.getBoundingClientRect()
      const { width, height } = mountRect
      if (width === 0 || height === 0) return
      renderer.setPixelRatio(Math.min(window.devicePixelRatio, 1.6))
      renderer.setSize(width, height, false)
      camera.aspect = width / height
      camera.updateProjectionMatrix()
      renderFrame()
    }

    const handleVisibilityChange = () => {
      isDocumentVisible = document.visibilityState !== 'hidden'
      syncAnimation()
    }
    const handleMotionPreference = (event: MediaQueryListEvent) => {
      reduceMotion = event.matches
      syncAnimation()
    }
    const handleViewportChange = () => {
      mountRect = mount.getBoundingClientRect()
    }

    const resizeObserver = new ResizeObserver(resizeScene)
    resizeObserver.observe(mount)
    const intersectionObserver = new IntersectionObserver(
      ([entry]) => {
        isInViewport = entry.isIntersecting
        syncAnimation()
      },
      { threshold: 0.05 },
    )
    intersectionObserver.observe(mount)

    window.addEventListener('pointermove', handlePointerMove, { passive: true })
    window.addEventListener('scroll', handleViewportChange, { passive: true })
    document.addEventListener('visibilitychange', handleVisibilityChange)
    motionPreference.addEventListener('change', handleMotionPreference)
    resizeScene()
    syncAnimation()

    return () => {
      renderer.setAnimationLoop(null)
      resizeObserver.disconnect()
      intersectionObserver.disconnect()
      window.removeEventListener('pointermove', handlePointerMove)
      window.removeEventListener('scroll', handleViewportChange)
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

  return <div ref={mountRef} className="size-full" aria-hidden="true" />
}
