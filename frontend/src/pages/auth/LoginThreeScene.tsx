import { useEffect, useRef } from 'react'
import * as THREE from 'three'

type AnimatedNode = {
  mesh: THREE.Mesh
  basePosition: THREE.Vector3
  speed: number
  phase: number
}

type AnimatedWave = {
  mesh: THREE.Mesh
  material: THREE.MeshBasicMaterial
  speed: number
}

const legacyColor = (hex: number) => new THREE.Color().setHex(hex, THREE.LinearSRGBColorSpace)

export function LoginThreeScene() {
  const mountRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const mount = mountRef.current
    if (!mount) return

    let randomSeed = 4_271
    const seededRandom = () => {
      randomSeed = (randomSeed * 16_807) % 2_147_483_647
      return (randomSeed - 1) / 2_147_483_646
    }

    const scene = new THREE.Scene()
    const camera = new THREE.PerspectiveCamera(45, 1, 0.1, 1_000)
    camera.position.set(0, 0, 19)

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
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2))
    renderer.outputColorSpace = THREE.SRGBColorSpace
    renderer.domElement.setAttribute('aria-hidden', 'true')
    renderer.domElement.style.display = 'block'
    renderer.domElement.style.height = '100%'
    renderer.domElement.style.width = '100%'
    mount.appendChild(renderer.domElement)

    const ambientLight = new THREE.AmbientLight(legacyColor(0xffffff), 0.85)
    const directionalLight = new THREE.DirectionalLight(legacyColor(0x60a5fa), 1.8)
    const rimLight = new THREE.DirectionalLight(legacyColor(0x3b82f6), 2.2)
    const pointLight = new THREE.PointLight(legacyColor(0x93c5fd), 1.5, 30)
    const highlightLight = new THREE.DirectionalLight(0xe8ffff, 1.6)
    directionalLight.position.set(10, 15, 12)
    rimLight.position.set(-12, -8, -10)
    pointLight.position.set(0, 2, 8)
    highlightLight.position.set(2, 4, 10)
    scene.add(ambientLight, directionalLight, rimLight, pointLight, highlightLight)

    const masterGroup = new THREE.Group()
    masterGroup.position.set(0, -2.2, 0)
    scene.add(masterGroup)

    const particleCount = 100
    const particlePositions = new Float32Array(particleCount * 3)

    for (let index = 0; index < particleCount; index += 1) {
      particlePositions[index * 3] = (seededRandom() - 0.5) * 32
      particlePositions[index * 3 + 1] = (seededRandom() - 0.5) * 22 - 1.5
      particlePositions[index * 3 + 2] = (seededRandom() - 0.5) * 16 - 2
    }

    const particleGeometry = new THREE.BufferGeometry()
    particleGeometry.setAttribute('position', new THREE.BufferAttribute(particlePositions, 3))
    const particleMaterial = new THREE.PointsMaterial({
      color: 0x4388cb,
      size: 0.12,
      transparent: true,
      opacity: 0.35,
      blending: THREE.AdditiveBlending,
      depthWrite: false,
    })
    const particles = new THREE.Points(particleGeometry, particleMaterial)
    scene.add(particles)

    const microphoneGroup = new THREE.Group()
    masterGroup.add(microphoneGroup)

    const metalMaterial = new THREE.MeshPhongMaterial({
      color: 0x315fc3,
      emissive: 0x0c245a,
      specular: 0xb9e2ff,
      shininess: 70,
    })
    const capsuleMaterial = new THREE.MeshPhongMaterial({
      color: 0x32e6ec,
      emissive: 0x05657a,
      specular: 0xffffff,
      shininess: 55,
    })
    const glowRingMaterial = new THREE.MeshBasicMaterial({
      color: 0x4de5f7,
      transparent: true,
      opacity: 0.85,
    })
    const nodeMaterial = new THREE.MeshPhongMaterial({
      color: 0xc7ffff,
      emissive: 0x28c9d8,
      specular: 0xffffff,
      shininess: 70,
    })
    const networkLineMaterial = new THREE.LineBasicMaterial({
      color: 0x326ec8,
      transparent: true,
      opacity: 0.35,
    })

    const capsule = new THREE.Mesh(new THREE.CylinderGeometry(1.05, 1.05, 2, 32), capsuleMaterial)
    capsule.position.y = 0.8

    const capsuleDome = new THREE.Mesh(
      new THREE.SphereGeometry(1.05, 32, 16, 0, Math.PI * 2, 0, Math.PI * 0.5),
      capsuleMaterial,
    )
    capsuleDome.position.y = 1.8

    const lowerBody = new THREE.Mesh(new THREE.CylinderGeometry(0.95, 0.8, 2.2, 32), metalMaterial)
    lowerBody.position.y = -1.1

    const accentRing = new THREE.Mesh(new THREE.TorusGeometry(1.08, 0.07, 16, 64), glowRingMaterial)
    accentRing.rotation.x = Math.PI / 2
    accentRing.position.y = -0.1

    const standRod = new THREE.Mesh(new THREE.CylinderGeometry(0.18, 0.18, 1.6, 16), metalMaterial)
    standRod.position.y = -2.8

    const standBase = new THREE.Mesh(new THREE.CylinderGeometry(1.35, 1.5, 0.3, 32), metalMaterial)
    standBase.position.y = -3.6

    microphoneGroup.add(capsule, capsuleDome, lowerBody, accentRing, standRod, standBase)

    const orbitalRingOne = new THREE.Mesh(
      new THREE.TorusGeometry(3.2, 0.03, 16, 100),
      new THREE.MeshBasicMaterial({
        color: 0x30b9db,
        transparent: true,
        opacity: 0.45,
      }),
    )
    orbitalRingOne.rotation.x = Math.PI / 3
    orbitalRingOne.rotation.y = Math.PI / 6

    const orbitalRingTwo = new THREE.Mesh(
      new THREE.TorusGeometry(3.7, 0.025, 16, 100),
      new THREE.MeshBasicMaterial({
        color: 0x6873da,
        transparent: true,
        opacity: 0.35,
      }),
    )
    orbitalRingTwo.rotation.x = -Math.PI / 4
    orbitalRingTwo.rotation.z = Math.PI / 5
    microphoneGroup.add(orbitalRingOne, orbitalRingTwo)

    const nodeCount = 14
    const animatedNodes: AnimatedNode[] = []
    const angleStep = (Math.PI * 2) / nodeCount

    for (let index = 0; index < nodeCount; index += 1) {
      const radius = 2.8 + (index % 3) * 0.8
      const angle = index * angleStep
      const nodeX = Math.cos(angle) * radius
      const nodeY = Math.sin(angle * 2) * 1.5 + (seededRandom() - 0.5) * 0.6
      const nodeZ = Math.sin(angle) * radius
      const basePosition = new THREE.Vector3(nodeX, nodeY, nodeZ)
      const node = new THREE.Mesh(
        new THREE.SphereGeometry(0.14 + (index % 3 === 0 ? 0.08 : 0), 16, 16),
        nodeMaterial,
      )
      node.position.copy(basePosition)
      microphoneGroup.add(node)
      animatedNodes.push({
        mesh: node,
        basePosition,
        speed: 0.6 + seededRandom() * 0.7,
        phase: seededRandom() * Math.PI * 2,
      })
    }

    const connectionPoints: number[] = []
    for (let startIndex = 0; startIndex < animatedNodes.length; startIndex += 1) {
      for (let endIndex = startIndex + 1; endIndex < animatedNodes.length; endIndex += 1) {
        const start = animatedNodes[startIndex].basePosition
        const end = animatedNodes[endIndex].basePosition

        if (start.distanceTo(end) < 3.4) {
          connectionPoints.push(start.x, start.y, start.z, end.x, end.y, end.z)
        }
      }
    }

    const connectionGeometry = new THREE.BufferGeometry()
    connectionGeometry.setAttribute(
      'position',
      new THREE.Float32BufferAttribute(connectionPoints, 3),
    )
    microphoneGroup.add(new THREE.LineSegments(connectionGeometry, networkLineMaterial))

    const animatedWaves: AnimatedWave[] = []
    for (let index = 0; index < 3; index += 1) {
      const material = new THREE.MeshBasicMaterial({
        color: 0x4f8bd7,
        transparent: true,
        opacity: 0.25 - index * 0.06,
        side: THREE.DoubleSide,
      })
      const wave = new THREE.Mesh(
        new THREE.RingGeometry(1.4 + index * 0.7, 1.44 + index * 0.7, 48),
        material,
      )
      wave.rotation.x = Math.PI / 2
      wave.position.y = 0.8
      microphoneGroup.add(wave)
      animatedWaves.push({ mesh: wave, material, speed: 1.2 + index * 0.3 })
    }

    const targetPointer = new THREE.Vector2()
    const smoothPointer = new THREE.Vector2()
    const motionPreference = window.matchMedia('(prefers-reduced-motion: reduce)')
    let reduceMotion = motionPreference.matches

    const renderFrame = (time = 0) => {
      const elapsed = time * 0.001

      if (!reduceMotion && document.visibilityState !== 'hidden') {
        smoothPointer.lerp(targetPointer, 0.05)
        masterGroup.position.x = -smoothPointer.x * 0.5
        masterGroup.position.y = -2.2 + smoothPointer.y * 0.45
        masterGroup.rotation.y = smoothPointer.x * 0.22
        masterGroup.rotation.x = -smoothPointer.y * 0.15

        particles.position.x = smoothPointer.x * 0.25
        particles.position.y = -smoothPointer.y * 0.2
        particles.rotation.y = elapsed * 0.015

        microphoneGroup.position.y = Math.sin(elapsed * 0.9) * 0.25
        microphoneGroup.rotation.y = elapsed * 0.15
        orbitalRingOne.rotation.z = elapsed * 0.18
        orbitalRingTwo.rotation.z = -elapsed * 0.14

        animatedNodes.forEach(({ mesh, basePosition, speed, phase }) => {
          const pulse = 1 + Math.sin(elapsed * speed + phase) * 0.15
          mesh.scale.setScalar(pulse)
          mesh.position.y = basePosition.y + Math.sin(elapsed * 1.2 + phase) * 0.12
        })

        animatedWaves.forEach(({ mesh, material, speed }) => {
          const waveScale = 1 + Math.sin(elapsed * speed) * 0.15
          mesh.scale.setScalar(waveScale)
          material.opacity = 0.2 + Math.sin(elapsed * speed) * 0.1
        })
      }

      renderer.render(scene, camera)
    }

    const syncAnimationLoop = () => {
      renderer.setAnimationLoop(
        reduceMotion || document.visibilityState === 'hidden' ? null : renderFrame,
      )
      renderFrame()
    }

    const handlePointerMove = (event: PointerEvent) => {
      const rect = mount.getBoundingClientRect()
      if (rect.width === 0 || rect.height === 0) return

      const relativeX = (event.clientX - rect.left) / rect.width
      const relativeY = (event.clientY - rect.top) / rect.height
      if (relativeX < 0 || relativeX > 1.2 || relativeY < 0 || relativeY > 1) return

      targetPointer.set((relativeX - 0.5) * 2, (relativeY - 0.5) * 2)
    }

    const resizeScene = () => {
      const { width, height } = mount.getBoundingClientRect()
      if (width === 0 || height === 0) return

      renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2))
      renderer.setSize(width, height, false)
      camera.aspect = width / height
      camera.updateProjectionMatrix()
      renderFrame()
    }

    const handleMotionPreference = (event: MediaQueryListEvent) => {
      reduceMotion = event.matches
      targetPointer.set(0, 0)
      smoothPointer.set(0, 0)
      syncAnimationLoop()
    }
    const handleVisibilityChange = () => syncAnimationLoop()

    const resizeObserver = new ResizeObserver(resizeScene)
    resizeObserver.observe(mount)
    window.addEventListener('pointermove', handlePointerMove, { passive: true })
    document.addEventListener('visibilitychange', handleVisibilityChange)
    motionPreference.addEventListener('change', handleMotionPreference)
    resizeScene()
    syncAnimationLoop()

    return () => {
      renderer.setAnimationLoop(null)
      resizeObserver.disconnect()
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
